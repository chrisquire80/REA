using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NoreaCli;

public static class NoreaBridge
{
    private static readonly string NotepadPipeId = "NotepadREA." + Environment.UserName;
    private static readonly string LauncherPipeId = "NoreaLauncher." + Environment.UserName;

    public static string DetectWorkspaceDir()
    {
        var cur = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(cur))
        {
            if (File.Exists(Path.Combine(cur, "norea_catalog.json")) ||
                Directory.Exists(Path.Combine(cur, "NotepadREA")))
                return cur;
            var parent = Path.GetDirectoryName(cur);
            if (parent == cur) break;
            cur = parent;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    public static string? FindExecutable(string appName)
    {
        string exeName = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appName : appName + ".exe";
        var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, exeName),
            Path.Combine(AppContext.BaseDirectory, "dist", exeName),
            Path.Combine(DetectWorkspaceDir(), "dist", exeName),
            Path.Combine(localAppData, Path.GetFileNameWithoutExtension(exeName), exeName),
            Path.Combine(localAppData, "NOREA", exeName),
            Path.Combine(localAppData, "NotepadREA", exeName)
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool OpenInNotepad(string filePath, int line = -1, int column = -1)
    {
        string fullPath = Path.GetFullPath(filePath);
        var args = new List<string>();
        if (line > 0) args.Add($"-n{line}");
        if (column > 0) args.Add($"-c{column}");
        args.Add(fullPath);

        // 1. Prova a inviare alla NamedPipe attiva di NotepadREA
        try
        {
            using var client = new NamedPipeClientStream(".", NotepadPipeId, PipeDirection.Out);
            client.Connect(400);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            w.Write(string.Join("\n", args));
            return true;
        }
        catch
        {
            // Nessuna istanza attiva: avvia l'eseguibile
            var exe = FindExecutable("NotepadREA");
            if (exe == null) return false;

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? ""
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
    }

    public static bool DiffInNotepad(string fileA, string fileB)
    {
        string fullA = Path.GetFullPath(fileA);
        string fullB = Path.GetFullPath(fileB);
        string[] args = ["--diff", fullA, fullB];

        try
        {
            using var client = new NamedPipeClientStream(".", NotepadPipeId, PipeDirection.Out);
            client.Connect(400);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            w.Write(string.Join("\n", args));
            return true;
        }
        catch
        {
            var exe = FindExecutable("NotepadREA");
            if (exe == null) return false;

            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
    }

    public static bool HexInNotepad(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string[] args = ["--hex", fullPath];

        try
        {
            using var client = new NamedPipeClientStream(".", NotepadPipeId, PipeDirection.Out);
            client.Connect(400);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            w.Write(string.Join("\n", args));
            return true;
        }
        catch
        {
            var exe = FindExecutable("NotepadREA");
            if (exe == null) return false;

            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
    }

    public static bool NotifyLauncher(string appName, string message, string oldVersion = "v2.1.0", string newVersion = "v2.2.0")
    {
        var exe = FindExecutable("NoreaLauncher");
        if (exe == null) return false;

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            ArgumentList = { "--notify", appName, message, oldVersion, newVersion }
        };
        Process.Start(psi);
        return true;
    }

    public static bool LaunchApp(string appId)
    {
        var exe = FindExecutable(appId);
        if (exe == null) return false;

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
        });
        return true;
    }

    public static List<Dictionary<string, object>> GetAppsStatus(bool runningOnly = false)
    {
        var result = new List<Dictionary<string, object>>();
        string[] appIds = ["NotepadREA", "WinSCPREA", "EverythingREA", "NoreaLauncher"];

        foreach (var id in appIds)
        {
            var exe = FindExecutable(id);
            var procs = Process.GetProcessesByName(id);
            bool isRunning = procs.Length > 0;
            int? pid = isRunning ? procs[0].Id : null;

            if (runningOnly && !isRunning) continue;

            string fallback = typeof(NoreaBridge).Assembly.GetName().Version?.ToString(3) ?? "2.5.0";
            string ver = "v" + fallback;
            if (exe != null && File.Exists(exe))
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exe);
                    ver = "v" + (fvi.ProductVersion?.Split('+')[0].TrimStart('v', 'V') ?? fallback);
                }
                catch { }
            }

            result.Add(new Dictionary<string, object>
            {
                ["id"] = id,
                ["name"] = id,
                ["version"] = ver,
                ["installed"] = exe != null,
                ["executablePath"] = exe ?? "",
                ["isRunning"] = isRunning,
                ["pid"] = pid ?? 0
            });
        }
        return result;
    }
}
