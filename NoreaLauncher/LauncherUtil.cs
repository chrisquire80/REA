using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NoreaLauncher;

public static class LauncherUtil
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static string AnonymizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData) && path.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
            return "%LocalAppData%" + path[localAppData.Length..];

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData) && path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            return "%AppData%" + path[appData.Length..];

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            return "%UserProfile%" + path[userProfile.Length..];

        return path;
    }

    public static (string Prefix, string Suffix) SplitReaName(string id)
    {
        if (string.IsNullOrEmpty(id)) return ("", "");
        if (id.EndsWith("REA", StringComparison.OrdinalIgnoreCase) && id.Length > 3)
            return (id[..^3], id[^3..].ToUpperInvariant());
        return (id, "");
    }

    public static string NormalizeVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion)) return "v1.0.0";
        string clean = rawVersion.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            return "v" + clean[1..].ToLowerInvariant();
        return "v" + clean.ToLowerInvariant();
    }

    public static string ComputeFileSha256(string path)
    {
        using var fs = File.OpenRead(path);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexStringLower(hash);
    }

    public static int CompareVersions(string v1, string v2)
    {
        string c1 = v1.TrimStart('v', 'V');
        string c2 = v2.TrimStart('v', 'V');
        if (Version.TryParse(c1, out var parsed1) && Version.TryParse(c2, out var parsed2))
            return parsed1.CompareTo(parsed2);
        return string.Compare(c1, c2, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathConfigured()
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string npDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NotepadREA");
            return paths.Any(p => string.Equals(p.TrimEnd('\\', '/'), npDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public static bool SetUserPathConfigured(bool enable)
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            string npDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NotepadREA");
            string noreaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NOREA");

            paths.RemoveAll(p => string.Equals(p.TrimEnd('\\', '/'), npDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(p.TrimEnd('\\', '/'), noreaDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));

            if (enable)
            {
                if (Directory.Exists(npDir)) paths.Add(npDir);
                if (Directory.Exists(noreaDir)) paths.Add(noreaDir);
            }

            Environment.SetEnvironmentVariable("Path", string.Join(";", paths), EnvironmentVariableTarget.User);
            BroadcastEnvironmentChange();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn("Errore durante la configurazione del PATH utente", ex);
            return false;
        }
    }

    public static void BroadcastEnvironmentChange()
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Environment",
                SMTO_ABORTIFHUNG,
                1500,
                out _);
        }
        catch { }
    }

    public static List<string> GetRecentFiles(string appId)
    {
        var list = new List<string>();
        try
        {
            if (string.Equals(appId, "NotepadREA", StringComparison.OrdinalIgnoreCase))
            {
                string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotepadREA", "settings.json");
                if (File.Exists(settingsPath))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));
                    if (doc.RootElement.TryGetProperty("RecentFiles", out var rf) && rf.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in rf.EnumerateArray())
                        {
                            string? p = item.GetString();
                            if (!string.IsNullOrEmpty(p) && File.Exists(p))
                            {
                                list.Add(p);
                                if (list.Count >= 5) break;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return list;
    }

    public static (double RamMb, TimeSpan Uptime, int ThreadCount)? GetProcessTelemetry(int? pid)
    {
        if (pid is null or <= 0) return null;
        try
        {
            using var p = Process.GetProcessById(pid.Value);
            if (p.HasExited) return null;

            double ramMb = Math.Round(p.WorkingSet64 / (1024.0 * 1024.0), 1);
            TimeSpan uptime = DateTime.Now - p.StartTime;
            int threads = p.Threads.Count;

            return (ramMb, uptime, threads);
        }
        catch { return null; }
    }
}
