using System.Text.Json;

namespace EverythingREA;

public sealed record FileIndexEntry(
    string Name,
    string Directory,
    string FullPath,
    bool IsDirectory,
    long SizeBytes,
    DateTime ModifiedUtc,
    string Extension);

public enum QuickCategory
{
    All,
    Folders,
    Files,
    Code,
    Documents,
    Images,
    Archives
}

public sealed record SearchOptions(
    bool MatchCase = false,
    bool MatchPath = false,
    bool UseRegex = false,
    QuickCategory Category = QuickCategory.All,
    int MaxResults = 200_000);

public enum SortField
{
    Name,
    Directory,
    Size,
    Modified,
    Extension
}

public sealed class EverythingSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<string> IndexedRoots { get; set; } = [];
    public List<string> ExcludedFolderNames { get; set; } =
    [
        "$Recycle.Bin",
        "System Volume Information",
        "WinSxS",
        ".git",
        "node_modules"
    ];
    public bool WatchFileSystem { get; set; } = true;
    public bool IncludeHidden { get; set; } = false;

    public static string SettingsPath
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("EVERYTHINGREA_SETTINGS_PATH");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EverythingREA", "settings.json")
                : Path.GetFullPath(overridePath);
        }
    }

    public static string IndexCachePath
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EverythingREA", "index.json")
                : Path.GetFullPath(overridePath);
        }
    }

    public static EverythingSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<EverythingSettings>(File.ReadAllText(SettingsPath), JsonOptions);
                if (loaded != null)
                {
                    loaded.IndexedRoots = NormalizeRoots(loaded.IndexedRoots);
                    loaded.ExcludedFolderNames ??= [];
                    if (loaded.IndexedRoots.Count == 0)
                        loaded.IndexedRoots = GetDefaultRoots();
                    return loaded;
                }
            }
        }
        catch
        {
        }

        return new EverythingSettings { IndexedRoots = GetDefaultRoots() };
    }

    public void Save()
    {
        string dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        string tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
        if (File.Exists(SettingsPath))
            File.Replace(tmp, SettingsPath, null);
        else
            File.Move(tmp, SettingsPath);
    }

    public static List<string> GetDefaultRoots()
    {
        var roots = new List<string>();
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
            roots.Add(userProfile);

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed &&
                    !string.Equals(drive.RootDirectory.FullName, Path.GetPathRoot(userProfile), StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
            }
            catch { }
        }

        return NormalizeRoots(roots);
    }

    public static List<string> NormalizeRoots(IEnumerable<string>? roots)
    {
        var result = new List<string>();
        if (roots == null) return result;
        foreach (var raw in roots)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            try
            {
                string full = Path.GetFullPath(raw.Trim());
                if (Directory.Exists(full) && !result.Contains(full, StringComparer.OrdinalIgnoreCase))
                    result.Add(full);
            }
            catch { }
        }
        return result;
    }
}
