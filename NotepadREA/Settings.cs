using System.Text.Json;

namespace NotepadREA;

/// <summary>Un buffer della sessione: file su disco e/o backup del contenuto non salvato.</summary>
public sealed class SessionEntry
{
    public string? Path { get; set; }
    public string? Backup { get; set; }
    public string? Title { get; set; }
    public FileEncoding Encoding { get; set; } = FileEncoding.Utf8;
    public string? Language { get; set; }
    public int Caret { get; set; }
    public int FirstLine { get; set; }
}

/// <summary>Impostazioni persistenti (equivalente semplificato di config.xml / session.xml di Notepad++).</summary>
public sealed class Settings
{
    public List<string> RecentFiles { get; set; } = [];
    public List<SessionEntry> Session { get; set; } = [];
    public List<string>? SessionFiles { get; set; } // formato v1, solo lettura per migrazione
    public int ActiveIndex { get; set; }
    public bool WordWrap { get; set; }
    public bool ShowWhitespace { get; set; }
    public bool ShowEol { get; set; }
    public bool ShowIndentGuides { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public bool SmartHighlight { get; set; } = true;
    public string? ThemeName { get; set; }
    public bool DarkMode { get; set; }  // formato v1
    public bool RestoreSession { get; set; } = true;
    /// <summary>Salvataggio periodico dei buffer non salvati e chiusura senza domande (come la "sessione snapshot" di Notepad++).</summary>
    public bool Snapshot { get; set; } = true;
    public int SnapshotSeconds { get; set; } = 7;
    public string FontName { get; set; } = "Consolas";
    public int FontSize { get; set; } = 10;
    public int Zoom { get; set; }
    public Rectangle? Bounds { get; set; }
    public bool Maximized { get; set; }

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotepadREA");
    public static string BackupDir => Path.Combine(Dir, "backup");
    static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    string? _lastSaved;

    public static Settings Load()
    {
        Settings s = new();
        try
        {
            if (File.Exists(FilePath)) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* impostazioni corrotte: riparti dai default */ }
        // Migrazione dal formato v1
        s.ThemeName ??= s.DarkMode ? Theme.Dark.Name : Theme.Light.Name;
        if (s.Session.Count == 0 && s.SessionFiles is { Count: > 0 } old) s.Session = old.Select(p => new SessionEntry { Path = p }).ToList();
        s.SessionFiles = null;
        return s;
    }

    /// <summary>Scrive su disco solo se qualcosa è cambiato (scrittura atomica via file temporaneo).</summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, Opts);
            if (json == _lastSaved) return;
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, FilePath, overwrite: true);
            _lastSaved = json;
        }
        catch { /* non bloccare l'editor */ }
    }

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 15) RecentFiles.RemoveRange(15, RecentFiles.Count - 15);
    }
}
