using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Win32;

namespace NoreaLauncher;

public static partial class CatalogService
{
    static bool IsExcludedSuiteComponent(string name) => CatalogPolicy.IsExcludedSuiteComponent(name);

    static bool IsExecutableProject(string csprojPath)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(csprojPath);
            var outType = doc.Descendants("OutputType").FirstOrDefault()?.Value?.Trim();
            return string.Equals(outType, "WinExe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outType, "Exe", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static void DiscoverWorkspaceProjects(string ws, List<AppEntry> list, LauncherSettings settings)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(ws))
            {
                var folderName = Path.GetFileName(dir);
                if (IsExcludedSuiteComponent(folderName)) continue;
                var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
                if (csproj is null) continue;

                var id = Path.GetFileNameWithoutExtension(csproj);
                if (IsExcludedSuiteComponent(id)) continue;
                if (!IsExecutableProject(csproj)) continue;

                var existing = list.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    var (prefix, suffix) = SplitReaName(id);
                    existing = new AppEntry
                    {
                        Id = id,
                        Prefix = prefix,
                        Suffix = suffix,
                        AuthorBadge = "BY NOREA",
                        Version = ReadCsprojVersion(csproj) ?? "V1.0.0",
                        Runtime = ".NET 10 WinForms",
                        Tagline = $"Applicazione {id} della suite NOREA",
                        Description = $"Progetto C#/.NET rilevato automaticamente nella cartella {folderName}.",
                        IconType = GuessIconType(id),
                        ProjectPath = folderName,
                        BinaryPath = Path.Combine("dist", id + ".exe"),
                        IsPlanned = false,
                    };
                    list.Insert(0, existing);
                }
                else
                {
                    existing.ProjectPath ??= folderName;
                    if (string.IsNullOrEmpty(existing.BinaryPath))
                        existing.BinaryPath = Path.Combine("dist", id + ".exe");
                    var ver = ReadCsprojVersion(csproj);
                    if (ver != null) existing.Version = ver;
                    existing.IsPlanned = false;
                }
            }

            var distDir = Path.Combine(ws, "dist");
            if (Directory.Exists(distDir))
            {
                foreach (var exe in Directory.GetFiles(distDir, "*.exe"))
                {
                    var id = Path.GetFileNameWithoutExtension(exe);
                    if (IsExcludedSuiteComponent(id)) continue;
                    var existing = list.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (existing is null)
                    {
                        var (prefix, suffix) = SplitReaName(id);
                        list.Insert(0, new AppEntry
                        {
                            Id = id,
                            Prefix = prefix,
                            Suffix = suffix,
                            AuthorBadge = "BY NOREA",
                            Version = "V1.0.0",
                            Runtime = "Win64 Native / .NET",
                            Tagline = $"Eseguibile autonomo {id}",
                            Description = $"Binario rilevato in dist\\{Path.GetFileName(exe)}.",
                            IconType = GuessIconType(id),
                            BinaryPath = Path.Combine("dist", Path.GetFileName(exe)),
                            IsPlanned = false,
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Errore scansione cartelle workspace", ex);
        }

        // Le app scoperte dinamicamente restano nella sessione in memoria senza sovrascrivere norea_catalog.json
    }

    static void ResolveAppState(AppEntry app, string ws, LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(app.ProjectPath))
        {
            var p = Path.IsPathRooted(app.ProjectPath) ? app.ProjectPath : Path.Combine(ws, app.ProjectPath);
            if (Directory.Exists(p)) app.ResolvedProjectDir = p;
        }

        if (!string.IsNullOrWhiteSpace(app.AnalysisPath))
        {
            var a = Path.IsPathRooted(app.AnalysisPath) ? app.AnalysisPath : Path.Combine(ws, app.AnalysisPath);
            if (File.Exists(a)) app.ResolvedAnalysisFile = a;
        }

        if (!string.IsNullOrWhiteSpace(app.InstallScript))
        {
            var s = Path.IsPathRooted(app.InstallScript) ? app.InstallScript : Path.Combine(ws, app.InstallScript);
            if (File.Exists(s)) app.ResolvedInstallScript = s;
        }

        if (!string.IsNullOrWhiteSpace(app.UninstallScript))
        {
            var u = Path.IsPathRooted(app.UninstallScript) ? app.UninstallScript : Path.Combine(ws, app.UninstallScript);
            if (File.Exists(u)) app.ResolvedUninstallScript = u;
        }

        string? distExe = null;
        if (!string.IsNullOrWhiteSpace(app.BinaryPath))
        {
            var cand = Path.IsPathRooted(app.BinaryPath) ? app.BinaryPath : Path.Combine(ws, app.BinaryPath);
            if (File.Exists(cand)) distExe = cand;
        }
        if (distExe is null && app.ResolvedProjectDir != null)
        {
            var relDir = Path.Combine(app.ResolvedProjectDir, "bin", "Release");
            if (Directory.Exists(relDir))
                distExe = Directory.GetFiles(relDir, app.Id + ".exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        if (distExe is null)
        {
            var beside = Path.Combine(AppContext.BaseDirectory, app.Id + ".exe");
            if (File.Exists(beside)) distExe = beside;
        }
        app.DistExePath = distExe;

        var localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        string[] candidates =
        [
            Path.Combine(localPrograms, app.Id, app.Id + ".exe"),
            Path.Combine(settings.InstallFolder, app.Id, app.Id + ".exe"),
            Path.Combine(settings.InstallFolder, app.Id + ".exe"),
            Path.Combine(AppContext.BaseDirectory, app.Id + ".exe"),
            Path.Combine(AppContext.BaseDirectory, "dist", app.Id + ".exe"),
        ];
        app.InstalledExePath = candidates.FirstOrDefault(File.Exists);

        var refFile = app.InstalledExePath ?? app.DistExePath;
        if (refFile != null && File.Exists(refFile))
        {
            var fi = new FileInfo(refFile);
            app.EstimatedSizeMb = Math.Round(fi.Length / (1024.0 * 1024.0), 1);
            app.LastUpdatedUtc = fi.LastWriteTimeUtc;
            app.LastUpdatedText = fi.LastWriteTime.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
            app.IsPlanned = false;
            try
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(refFile);
                var rawVer = !string.IsNullOrWhiteSpace(fvi.ProductVersion) ? fvi.ProductVersion : fvi.FileVersion;
                if (!string.IsNullOrWhiteSpace(rawVer))
                {
                    var clean = rawVer.Split('+')[0].Trim();
                    if (!string.IsNullOrEmpty(clean))
                        app.Version = "v" + clean.TrimStart('v', 'V');
                }
            }
            catch { }
        }
        else if (app.ResolvedProjectDir != null)
        {
            var di = new DirectoryInfo(app.ResolvedProjectDir);
            app.LastUpdatedUtc = di.LastWriteTimeUtc;
            app.LastUpdatedText = di.LastWriteTime.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
        }
    }

    public static (string Prefix, string Suffix) SplitReaName(string id) => LauncherUtil.SplitReaName(id);

    static string? ReadCsprojVersion(string csprojPath)
    {
        try
        {
            var direct = ReadConcreteVersion(XDocument.Load(csprojPath));
            if (direct != null) return direct;

            var dir = Path.GetDirectoryName(Path.GetFullPath(csprojPath));
            while (!string.IsNullOrEmpty(dir))
            {
                var propsPath = Path.Combine(dir, "Directory.Build.props");
                if (File.Exists(propsPath))
                {
                    var props = XDocument.Load(propsPath);
                    var suite = props.Descendants("NoreaSuiteVersion").FirstOrDefault()?.Value;
                    if (IsConcreteVersion(suite))
                        return "v" + suite!.Trim().TrimStart('v', 'V');
                    return ReadConcreteVersion(props);
                }

                var parent = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                    break;
                dir = parent;
            }
        }
        catch { }
        return null;
    }

    static string? ReadConcreteVersion(XDocument doc)
    {
        var value = doc.Descendants("Version").FirstOrDefault()?.Value;
        if (!IsConcreteVersion(value)) return null;
        return "v" + value!.Trim().TrimStart('v', 'V');
    }

    static bool IsConcreteVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("$(");

    static string GuessIconType(string id) =>
        id.Contains("note", StringComparison.OrdinalIgnoreCase) ? "notepad" :
        id.Contains("winscp", StringComparison.OrdinalIgnoreCase) || id.Contains("transfer", StringComparison.OrdinalIgnoreCase) ? "transfer" :
        id.Contains("everything", StringComparison.OrdinalIgnoreCase) || id.Contains("search", StringComparison.OrdinalIgnoreCase) ? "search" :
        "app";

    static List<AppEntry> GetDefaultCatalog() =>
    [
        new()
        {
            Id = "NotepadREA", Prefix = "Notepad", Suffix = "REA", AuthorBadge = "BY NOREA", Version = "v2.6.0",
            Runtime = "Scintilla/Lexilla · .NET 10",
            Tagline = "Editor di testo e codice Scintilla/Lexilla",
            Description = "Ricostruzione completa da zero basata sull'analisi statica REA di Notepad++ 8.9.8.1.",
            IconType = "notepad", AccentHex = "#60A5FA", ProjectPath = "NotepadREA", BinaryPath = @"dist\NotepadREA.exe",
            AnalysisPath = "ANALISI_REA.md", InstallScript = "installa_associazioni.ps1", UninstallScript = "rimuovi_associazioni.ps1",
            EstimatedSizeMb = 53.7, LastUpdatedText = "10 nov 2026", IsPlanned = false,
            Analysis = new AnalysisSection
            {
                Title = "Esito Analisi Statica (Notepad++ 8.9.8.1 Portable)",
                Rows =
                [
                    new AnalysisRow { Label = "Target analizzato:", Value = "notepad++.exe (PE32+, AMD64, 8.5 MB, Authenticode firmato)" },
                    new AnalysisRow { Label = "Motore individuato:", Value = "Scintilla + Lexilla linkati staticamente (nessuna DLL esterna)" },
                    new AnalysisRow { Label = "Hardening:", Value = "Control Flow Guard attivo, Security Cookie /GS presente" },
                    new AnalysisRow { Label = "Ricostruzione NOREA:", Value = "C# .NET 10 WinForms con Scintilla5.NET (zero codice sorgente Notepad++)" }
                ]
            }
        },
        new()
        {
            Id = "WinSCPREA", Prefix = "WinSCP", Suffix = "REA", AuthorBadge = "BY NOREA", Version = "v1.0.2",
            Runtime = "SSH.NET · .NET 10",
            Tagline = "Client SFTP e file manager a due pannelli",
            Description = "Ricostruzione nativa da zero basata sull'analisi statica di WinSCP 6.7.1.",
            IconType = "transfer", AccentHex = "#6DA9FF", ProjectPath = "WinSCPREA", BinaryPath = @"dist\WinSCPREA.exe",
            AnalysisPath = "ANALISI_WINSCP_REA.md", EstimatedSizeMb = 6.0, LastUpdatedText = "18 nov 2026", IsPlanned = false,
            Analysis = new AnalysisSection
            {
                Title = "Esito Analisi Statica (WinSCP 6.7.1)",
                Rows =
                [
                    new AnalysisRow { Label = "Target analizzato:", Value = "WinSCP.exe (PE32, x86, 20,4 MB, firmato)" },
                    new AnalysisRow { Label = "Ricostruzione NOREA:", Value = "C# .NET 10 WinForms, SFTP con SSH.NET" }
                ]
            },
            UsageGuide = new UsageGuide
            {
                Title = "Guida rapida · CLI SFTP",
                Introduction = "WinSCPREA si usa anche dal terminale con winscprea. Per il primo avvio apri PowerShell nella cartella WinSCPREA\\agent-harness e installa il pacchetto nel virtualenv del progetto.",
                Examples =
                [
                    new UsageExample { Title = "Prima installazione", Command = "py -3 -m venv .venv\n.\\.venv\\Scripts\\Activate.ps1\npython -m pip install -e .", Description = "Esegui una volta dalla cartella WinSCPREA\\agent-harness." },
                    new UsageExample { Title = "Apri la guida o la REPL", Command = "winscprea --help\nwinscprea", Description = "La REPL accetta gli stessi comandi; digita help o exit." },
                    new UsageExample { Title = "Registra un sito e verifica l'elenco", Command = "winscprea site add staging --host sftp.example.net --username deploy\nwinscprea site list", Description = "Sostituisci i valori di esempio con i dati del server. Le password non vengono salvate nel profilo." },
                    new UsageExample { Title = "Elenca e trasferisci file", Command = "winscprea remote list --site staging --path /incoming\nwinscprea upload --site staging --remote-dir /incoming .\\release.zip\nwinscprea download --site staging --remote-path /incoming/report.csv --local-dir .\\downloads", Description = "I trasferimenti non sovrascrivono i file esistenti." },
                    new UsageExample { Title = "Usa output per agenti", Command = "winscprea --json site list", Description = "--json restituisce una risposta JSON leggibile da script e agenti." }
                ],
                Notes =
                [
                    "Nelle sessioni interattive la password viene richiesta senza mostrarla; gli script possono usare WINSCPREA_PASSWORD. Per chiavi cifrate imposta WINSCPREA_KEY_PASSPHRASE.",
                    "Alla prima connessione verifica l'impronta SHA-256 con il gestore del server, poi ripeti con --trust-host-key e l'impronta esatta.",
                    "Rinomina ed eliminazione remota richiedono --yes; usa --dry-run per controllare una modifica prima di eseguirla."
                ]
            }
        },
        new()
        {
            Id = "EverythingREA", Prefix = "Everything", Suffix = "REA", AuthorBadge = "BY NOREA", Version = "v1.0.0",
            Runtime = "Indicizzazione parallela · .NET 10",
            Tagline = "Motore di ricerca istantanea file e cartelle",
            Description = "Ricostruzione nativa da zero basata sull'analisi statica di Everything 1.4.1.1032.",
            IconType = "search", AccentHex = "#6DA9FF", ProjectPath = "EverythingREA", BinaryPath = @"dist\EverythingREA.exe",
            AnalysisPath = "ANALISI_EVERYTHING_REA.md", EstimatedSizeMb = 1.2, LastUpdatedText = "25 nov 2026", IsPlanned = false,
            Analysis = new AnalysisSection
            {
                Title = "Esito analisi statica (Everything 1.4.1.1032 x64)",
                Rows =
                [
                    new AnalysisRow { Label = "Target analizzato:", Value = "Everything.exe (PE32+, AMD64, 2,27 MB, Authenticode voidtools PTY LTD)" },
                    new AnalysisRow { Label = "Ricostruzione NOREA:", Value = "C# .NET 10 WinForms con indicizzazione parallela e cache locale" }
                ]
            }
        }
    ];
}
