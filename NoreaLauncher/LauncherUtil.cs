using System.Diagnostics;
using System.Security.Cryptography;

namespace NoreaLauncher;

public static class LauncherUtil
{
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
}
