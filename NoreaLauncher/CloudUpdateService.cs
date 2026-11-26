using System.IO.Compression;
using System.Diagnostics;
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
    DateTime PublishedAt,
    string? AssetSha256 = null);

public sealed record UpdateApplyResult(bool Success, string Message, bool RestartRequired = false);

public sealed record CloudUpdateCheck(CloudReleaseInfo? Release, int? StatusCode, string? Error)
{
    public bool NotFound => StatusCode == 404;
    public bool Succeeded => Error is null;
}

public static class CloudUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private const long MaxArchiveBytes = 512L * 1024 * 1024;
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;
    private const int MaxArchiveEntries = 10_000;

    static CloudUpdateService()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "NoreaLauncher-CloudUpdate/1.0");
        Http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public static async Task<CloudUpdateCheck> CheckForUpdatesAsync(
        string repo,
        string currentVersion,
        bool includePreRelease = false,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/'))
            return new CloudUpdateCheck(null, null, "Repository GitHub non valido.");

        try
        {
            string url = $"https://api.github.com/repos/{repo.Trim()}/releases";
            httpClient ??= Http;
            using var response = await httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                string message = $"Il repository GitHub \"{repo.Trim()}\" non esiste o non è pubblico (HTTP 404).";
                Log.Warn(message);
                return new CloudUpdateCheck(null, 404, message);
            }
            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"Verifica aggiornamenti cloud fallita: HTTP {response.StatusCode}");
                return new CloudUpdateCheck(null, (int)response.StatusCode, $"Verifica aggiornamenti non riuscita: HTTP {(int)response.StatusCode}.");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new CloudUpdateCheck(null, 200, "Risposta GitHub non valida.");

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
                string? assetSha256 = null;
                long assetSize = 0;
                if (rel.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetSize = asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0;
                        if (asset.TryGetProperty("digest", out var digestElement) &&
                            digestElement.ValueKind == JsonValueKind.String)
                        {
                            string? digest = digestElement.GetString();
                            if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
                                assetSha256 = digest["sha256:".Length..];
                        }
                        break;
                    }
                }

                string normalizedRemote = LauncherUtil.NormalizeVersion(tag);
                string normalizedLocal = LauncherUtil.NormalizeVersion(currentVersion);
                bool isNewer = LauncherUtil.CompareVersions(normalizedRemote, normalizedLocal) > 0;

                return new CloudUpdateCheck(new CloudReleaseInfo(
                    IsUpdateAvailable: isNewer,
                    RemoteVersion: normalizedRemote,
                    CurrentVersion: normalizedLocal,
                    Title: title,
                    Changelog: body,
                    DownloadUrl: downloadUrl,
                    AssetSizeBytes: assetSize,
                    ReleasePageUrl: htmlUrl,
                    PublishedAt: pubDate,
                    AssetSha256: assetSha256), 200, null);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Errore durante la verifica aggiornamenti cloud", ex);
            return new CloudUpdateCheck(null, null, "Verifica aggiornamenti non riuscita.");
        }

        return new CloudUpdateCheck(null, 200, null);
    }

    public static async Task<UpdateApplyResult> DownloadAndApplyUpdateAsync(
        CloudReleaseInfo release,
        string targetDir,
        IProgress<int>? progress = null,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(release.DownloadUrl))
            return new(false, "Nessun pacchetto ZIP disponibile per questa release.");
        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return new(false, "URL del pacchetto non valido o non proveniente da GitHub.");
        if (string.IsNullOrWhiteSpace(release.AssetSha256) || release.AssetSha256.Length != 64 ||
            !release.AssetSha256.All(Uri.IsHexDigit))
            return new(false, "La release non fornisce un digest SHA-256 valido per il pacchetto.");
        if (release.AssetSizeBytes > MaxArchiveBytes)
            return new(false, "Il pacchetto supera la dimensione massima consentita.");

        string tempZip = Path.Combine(Path.GetTempPath(), $"norea_update_{Guid.NewGuid():N}.zip");
        httpClient ??= Http;
        bool handedOff = false;

        try
        {
            using (var response = await httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                if (response.RequestMessage?.RequestUri is { } finalUri && !IsTrustedGitHubHost(finalUri.Host))
                    return new(false, "Il download è stato reindirizzato a un host non attendibile.");

                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength > 0 && release.AssetSizeBytes > 0 && contentLength != release.AssetSizeBytes)
                    return new(false, "La dimensione del pacchetto non corrisponde a quella dichiarata dalla release.");
                long totalBytes = contentLength ?? release.AssetSizeBytes;
                if (totalBytes > MaxArchiveBytes)
                    return new(false, "Il pacchetto supera la dimensione massima consentita.");

                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    if (totalRead + read > MaxArchiveBytes)
                        return new(false, "Il download supera la dimensione massima consentita.");
                    await file.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;
                    if (totalBytes > 0) progress?.Report((int)Math.Min(100, totalRead * 100 / totalBytes));
                }
                if (totalBytes > 0 && totalRead != totalBytes)
                    return new(false, "La dimensione del download non corrisponde a quella dichiarata.");
                if (release.AssetSizeBytes > 0 && totalRead != release.AssetSizeBytes)
                    return new(false, "La dimensione del download non corrisponde a quella dichiarata dalla release.");
            }

            var applied = await ApplyUpdateArchiveAsync(tempZip, targetDir, release.AssetSha256);
            handedOff = applied.RestartRequired;
            return applied.Success && !applied.RestartRequired
                ? new UpdateApplyResult(true, $"Aggiornamento alla versione {release.RemoteVersion} completato con successo.")
                : applied;
        }
        catch (Exception ex)
        {
            Log.Error("Errore durante il download o l'estrazione dell'aggiornamento cloud", ex);
            return new(false, $"Errore durante l'aggiornamento: {ex.Message}");
        }
        finally
        {
            if (!handedOff) TryDeleteFile(tempZip);
        }
    }

    public static Task<UpdateApplyResult> ApplyUpdateArchiveAsync(
        string archivePath,
        string targetDir,
        string? expectedSha256 = null) =>
        Task.Run(() => ApplyUpdateArchive(archivePath, targetDir, expectedSha256));

    public static UpdateApplyResult ApplyDeferredUpdate(string archivePath, string targetDir, string? expectedSha256, int parentProcessId, string stagingDir)
    {
        WaitForProcessExit(parentProcessId);
        UpdateApplyResult result;
        try
        {
            result = ApplyUpdateArchive(archivePath, targetDir, expectedSha256);
            string launcher = Path.Combine(Path.GetFullPath(targetDir), "NoreaLauncher.exe");
            if (File.Exists(launcher))
            {
                var start = new ProcessStartInfo(launcher) { UseShellExecute = true, WorkingDirectory = targetDir };
                start.ArgumentList.Add("--cleanup-update");
                start.ArgumentList.Add(stagingDir);
                start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Process.Start(start);
            }
            return result;
        }
        finally
        {
            TryDeleteFile(archivePath);
        }
    }

    public static void WaitForProcessExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit();
        }
        catch (ArgumentException) { }
    }

    private static UpdateApplyResult ApplyUpdateArchive(string archivePath, string targetDir, string? expectedSha256)
    {
        string workDir = Path.Combine(Path.GetTempPath(), $"norea_update_{Guid.NewGuid():N}");
        string extractedDir = Path.Combine(workDir, "extracted");
        string backupDir = Path.Combine(workDir, "backup");
        var appliedFiles = new List<(string Destination, string? Backup)>();
        bool handedOff = false;

        try
        {
            if (!File.Exists(archivePath)) return new(false, "Archivio di aggiornamento non trovato.");
            if (new FileInfo(archivePath).Length > MaxArchiveBytes)
                return new(false, "Il pacchetto supera la dimensione massima consentita.");
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                string actualHash = LauncherUtil.ComputeFileSha256(archivePath);
                if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    return new(false, "Verifica SHA-256 del pacchetto fallita; aggiornamento annullato.");
            }

            string root = Path.GetFullPath(targetDir);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(extractedDir);
            Directory.CreateDirectory(backupDir);

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > MaxArchiveEntries)
                    return new(false, "Il pacchetto contiene troppe voci.");

                long expandedBytes = 0;
                var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
                        relative.Split(Path.DirectorySeparatorChar).Any(part =>
                            part is "." or ".." || part.Contains(':')))
                        return new(false, $"Percorso non valido nell'archivio: {entry.FullName}");

                    string destination = Path.GetFullPath(Path.Combine(extractedDir, relative));
                    if (!destination.StartsWith(extractedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        return new(false, $"Percorso fuori dalla directory di estrazione: {entry.FullName}");
                    if (!destinations.Add(relative.TrimEnd(Path.DirectorySeparatorChar)))
                        return new(false, $"Voce duplicata nell'archivio: {entry.FullName}");
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        return new(false, $"Link simbolico non consentito nell'archivio: {entry.FullName}");

                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    expandedBytes += entry.Length;
                    if (expandedBytes > MaxExpandedBytes)
                        return new(false, "Il contenuto estratto supera la dimensione massima consentita.");

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var input = entry.Open();
                    using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    input.CopyTo(output);
                }
            }

            var files = Directory.GetFiles(extractedDir, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0) return new(false, "Il pacchetto non contiene file da aggiornare.");

            string launcherPath = Path.Combine(root, "NoreaLauncher.exe");
            string stagedLauncherPath = Path.Combine(extractedDir, "NoreaLauncher.exe");
            if (File.Exists(stagedLauncherPath) &&
                string.Equals(Environment.ProcessPath is { } currentPath ? Path.GetFullPath(currentPath) : null,
                    Path.GetFullPath(launcherPath), StringComparison.OrdinalIgnoreCase))
            {
                var helper = new ProcessStartInfo(stagedLauncherPath) { UseShellExecute = true, WorkingDirectory = extractedDir };
                helper.ArgumentList.Add("--apply-update");
                helper.ArgumentList.Add(Path.GetFullPath(archivePath));
                helper.ArgumentList.Add(root);
                helper.ArgumentList.Add(expectedSha256 ?? "");
                helper.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                helper.ArgumentList.Add(workDir);
                if (Process.Start(helper) is null)
                    throw new InvalidOperationException("Impossibile avviare il processo che applicherà l'aggiornamento.");
                handedOff = true;
                return new(true, "Aggiornamento verificato: il launcher si chiuderà e verrà riavviato dopo l'applicazione.", true);
            }

            for (int i = 0; i < files.Length; i++)
            {
                string relative = Path.GetRelativePath(extractedDir, files[i]);
                string destination = Path.GetFullPath(Path.Combine(root, relative));
                if (!destination.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Destinazione fuori dalla cartella di installazione: {relative}");

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string temporaryDestination = destination + $".{Guid.NewGuid():N}.tmp";
                string? backup = null;
                try
                {
                    File.Copy(files[i], temporaryDestination);
                    if (File.Exists(destination))
                    {
                        backup = Path.Combine(backupDir, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        File.Copy(destination, backup);
                    }

                    File.Move(temporaryDestination, destination, overwrite: true);
                    appliedFiles.Add((destination, backup));
                }
                finally
                {
                    TryDeleteFile(temporaryDestination);
                }
            }

            return new(true, "Aggiornamento applicato.");
        }
        catch (Exception ex)
        {
            Log.Error("Applicazione aggiornamento fallita; avvio rollback.", ex);
            var rollbackErrors = new List<string>();
            foreach (var file in appliedFiles.AsEnumerable().Reverse())
            {
                string? restore = null;
                try
                {
                    if (file.Backup is null)
                    {
                        if (File.Exists(file.Destination)) File.Delete(file.Destination);
                    }
                    else
                    {
                        restore = file.Destination + $".{Guid.NewGuid():N}.restore";
                        File.Copy(file.Backup, restore);
                        File.Move(restore, file.Destination, overwrite: true);
                    }
                }
                catch (Exception rollbackEx)
                {
                    rollbackErrors.Add($"{file.Destination}: {rollbackEx.Message}");
                    Log.Error($"Rollback fallito per {file.Destination}.", rollbackEx);
                }
                finally
                {
                    if (restore is not null) TryDeleteFile(restore);
                }
            }

            string message = $"Aggiornamento non applicato: {ex.Message}";
            if (rollbackErrors.Count > 0)
                message += " Il rollback non è stato completato per: " + string.Join("; ", rollbackErrors);
            return new(false, message);
        }
        finally
        {
            if (!handedOff)
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); }
                catch (Exception ex) { Log.Warn($"Impossibile eliminare i file temporanei {workDir}.", ex); }
            }
        }
    }

    private static bool IsTrustedGitHubHost(string host) =>
        string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch (Exception ex) { Log.Warn($"Impossibile eliminare il file temporaneo {path}.", ex); }
    }
}
