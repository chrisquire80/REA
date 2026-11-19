using System.Text.Json;

namespace WinSCPREA;

public sealed class SiteProfile
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string RemoteDirectory { get; set; } = "/";
    public string Authentication { get; set; } = "Password";
    public string PrivateKeyPath { get; set; } = "";
}

public sealed class WinScpSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<SiteProfile> Sites { get; set; } = [];
    public Dictionary<string, string> KnownHosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string SettingsPath
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("WINSCPREA_SETTINGS_PATH");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinSCPREA", "settings.json")
                : Path.GetFullPath(overridePath);
        }
    }

    public static WinScpSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new WinScpSettings();
            var settings = JsonSerializer.Deserialize<WinScpSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? new WinScpSettings();
            settings.Sites ??= [];
            settings.KnownHosts ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return settings;
        }
        catch
        {
            return new WinScpSettings();
        }
    }

    public void Save()
    {
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        string tempPath = SettingsPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(this, JsonOptions));
        if (File.Exists(SettingsPath))
            File.Replace(tempPath, SettingsPath, null);
        else
            File.Move(tempPath, SettingsPath);
    }

    public static string HostKeyId(string host, int port) => $"{host.Trim().ToLowerInvariant()}:{port}";
}

public sealed record FileEntry(string Name, string FullPath, bool IsDirectory, long Length, DateTime Modified);

public sealed record RemoteFileEntry(string Name, string FullPath, bool IsDirectory, long Length, DateTime Modified);

public sealed record ConnectionRequest(
    SiteProfile Site,
    string Password,
    string KeyPassphrase);
