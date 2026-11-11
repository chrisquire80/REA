using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace NoreaLauncher;

public sealed record CloudReleaseInfo(
    bool IsUpdateAvailable,
    string RemoteVersion,
    string CurrentVersion,
    string Title,
    string Changelog,
    string? DownloadUrl,
    long AssetSizeBytes,
    string ReleasePageUrl,
    DateTime PublishedAt);

public static class CloudUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static CloudUpdateService()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NoreaLauncher-CloudUpdate/1.0");
        Http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public static async Task<CloudReleaseInfo?> CheckForUpdatesAsync(string repo, string currentVersion, bool includePreRelease = false)
    {
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/'))
            return null;

        try
        {
            string url = $"https://api.github.com/repos/{repo.Trim()}/releases";
            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"Verifica aggiornamenti cloud fallita: HTTP {response.StatusCode}");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                bool isPre = rel.TryGetProperty("prerelease", out var pr) && pr.GetBoolean();
                if (isPre && !includePreRelease) continue;

                string tag = rel.GetProperty("tag_name").GetString() ?? "";
                string title = rel.TryGetProperty("name", out var n) ? (n.GetString() ?? tag) : tag;
                string body = rel.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
                string htmlUrl = rel.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "";
                DateTime pubDate = rel.TryGetProperty("published_at", out var d) && d.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow;

                string? downloadUrl = null;
                long assetSize = 0;

                if (rel.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            assetSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                            break;
                        }
                    }
                }

                string normalizedRemote = LauncherUtil.NormalizeVersion(tag);
                string normalizedLocal = LauncherUtil.NormalizeVersion(currentVersion);
                bool isNewer = LauncherUtil.CompareVersions(normalizedRemote, normalizedLocal) > 0;

                return new CloudReleaseInfo(
                    IsUpdateAvailable: isNewer,
                    RemoteVersion: normalizedRemote,
                    CurrentVersion: normalizedLocal,
                    Title: title,
                    Changelog: body,
                    DownloadUrl: downloadUrl,
                    AssetSizeBytes: assetSize,
                    ReleasePageUrl: htmlUrl,
                    PublishedAt: pubDate
                );
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Errore durante la verifica aggiornamenti cloud", ex);
        }

        return null;
    }

    public static async Task<(bool Success, string Message)> DownloadAndApplyUpdateAsync(
        CloudReleaseInfo release,
        string targetDir,
        IProgress<int>? progress = null)
    {
        if (string.IsNullOrEmpty(release.DownloadUrl))
            return (false, "Nessun pacchetto ZIP disponibile per questa release.");

        string tempZip = Path.Combine(Path.GetTempPath(), $"norea_update_{Guid.NewGuid():N}.zip");
        string tempExtract = Path.Combine(Path.GetTempPath(), $"norea_extracted_{Guid.NewGuid():N}");

        try
        {
            using (var res = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                res.EnsureSuccessStatusCode();
                long totalBytes = res.Content.Headers.ContentLength ?? release.AssetSizeBytes;

                using var stream = await res.Content.ReadAsStreamAsync();
                using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;
                    if (totalBytes > 0 && progress != null)
                    {
                        int pct = (int)((totalRead * 100) / totalBytes);
                        progress.Report(pct);
                    }
                }
            }

            // Estrazione archivio
            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

            // Copia dei file aggiornati
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(tempExtract, "*.*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(tempExtract, file);
                string dest = Path.Combine(targetDir, rel);

                string? dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // Se è l'eseguibile del launcher attualmente in uso, salviamo come .new per non bloccare
                if (string.Equals(Path.GetFileName(file), "NoreaLauncher.exe", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                    catch (IOException)
                    {
                        File.Copy(file, dest + ".new", overwrite: true);
                    }
                }
                else
                {
                    try
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Impossibile sovrascrivere {dest}", ex);
                    }
                }
            }

            return (true, $"Aggiornamento alla versione {release.RemoteVersion} completato con successo!");
        }
        catch (Exception ex)
        {
            Log.Error("Errore durante il download o l'estrazione dell'aggiornamento cloud", ex);
            return (false, $"Errore durante l'aggiornamento: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, recursive: true); } catch { }
        }
    }
}
