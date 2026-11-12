using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace NoreaCli;

public sealed record AiSetupOptions(
    string UserHome,
    string InstallDirectory,
    string? CurrentExecutable,
    bool RegisterUserPath = true,
    bool InstallCli = true);

public static class AiSetup
{
    public static (bool Success, string Message) RunSetup(AiSetupOptions? options = null)
    {
        options ??= new AiSetupOptions(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NOREA"),
            Environment.ProcessPath);

        var logs = new List<string>();
        bool success = true;

        RunStep("CLI/PATH", () => EnsureInUserPath(options), logs, ref success);
        RunStep("Claude Code", () => ConfigureClaudeCode(options.UserHome), logs, ref success);
        RunStep("Antigravity", () => ConfigureAntigravity(options.UserHome), logs, ref success);

        return (success, string.Join(Environment.NewLine, logs));
    }

    private static void RunStep(string name, Func<string> action, List<string> logs, ref bool success)
    {
        try
        {
            logs.Add(action());
        }
        catch (Exception ex)
        {
            success = false;
            logs.Add($"Errore {name}: {ex.Message}");
        }
    }

    private static string EnsureInUserPath(AiSetupOptions options)
    {
        if (options.InstallCli || options.RegisterUserPath)
            Directory.CreateDirectory(options.InstallDirectory);
        string targetExe = Path.Combine(options.InstallDirectory, "norea.exe");
        string targetCmd = Path.Combine(options.InstallDirectory, "norea.cmd");

        if (options.InstallCli && !string.IsNullOrWhiteSpace(options.CurrentExecutable) &&
            File.Exists(options.CurrentExecutable) &&
            !string.Equals(options.CurrentExecutable, targetExe, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(options.CurrentExecutable, targetExe, overwrite: true);
        }

        if (options.InstallCli)
            WriteAtomically(targetCmd, "@echo off\r\n\"%~dp0norea.exe\" %*\r\n");

        if (options.RegisterUserPath)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Environment", writable: true)
                ?? throw new InvalidOperationException("Impossibile aprire il registro dell'ambiente utente.");
            string currentPath = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
            var segments = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!segments.Any(s => string.Equals(s.Trim(), options.InstallDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                segments.Add(options.InstallDirectory);
                key.SetValue("Path", string.Join(";", segments), RegistryValueKind.ExpandString);
            }
        }

        return $"✓ CLI NOREA configurata in {options.InstallDirectory}" +
               (options.RegisterUserPath ? " e registrata nel PATH utente." : ".");
    }

    private static string ConfigureClaudeCode(string userHome)
    {
        string claudeConfigDir = Path.Combine(userHome, ".claude");
        Directory.CreateDirectory(claudeConfigDir);

        string mcpConfigPath = Path.Combine(claudeConfigDir, "mcp.json");
        JsonObject root = ReadJsonObject(mcpConfigPath);
        JsonObject servers = GetOrCreateObject(root, "mcpServers");
        servers["norea"] = CreateServerEntry();
        WriteAtomically(mcpConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        string globalClaudeJson = Path.Combine(userHome, ".claude.json");
        if (File.Exists(globalClaudeJson))
        {
            JsonObject global = ReadJsonObject(globalClaudeJson);
            GetOrCreateObject(global, "mcpServers")["norea"] = CreateServerEntry();
            WriteAtomically(globalClaudeJson, global.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        return $"✓ Server MCP NOREA configurato per Claude Code ({mcpConfigPath}).";
    }

    private static string ConfigureAntigravity(string userHome)
    {
        string mcpDir = Path.Combine(userHome, ".gemini", "antigravity", "mcp", "norea");
        Directory.CreateDirectory(mcpDir);

        var configObj = new JsonObject
        {
            ["name"] = "norea",
            ["command"] = "norea",
            ["args"] = new JsonArray { "mcp" },
            ["description"] = "Suite NOREA (NotepadREA editor, visual side-by-side diff, hex inspection, desktop toast notifications)"
        };

        string configPath = Path.Combine(mcpDir, "server.json");
        WriteAtomically(configPath, configObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return $"✓ Server MCP NOREA configurato per Google Antigravity ({configPath}).";
    }

    private static JsonObject ReadJsonObject(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new JsonException($"Il file {path} non contiene un oggetto JSON.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new InvalidDataException($"Configurazione JSON non valida; il file originale è stato preservato: {path}", ex);
        }
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject child) return child;
        if (parent[name] != null)
            throw new InvalidDataException($"La proprietà '{name}' deve essere un oggetto JSON.");
        child = [];
        parent[name] = child;
        return child;
    }

    private static JsonObject CreateServerEntry() => new()
    {
        ["command"] = "norea",
        ["args"] = new JsonArray { "mcp" }
    };

    private static void WriteAtomically(string path, string content)
    {
        string temp = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, content);
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, null); }
                catch (PlatformNotSupportedException) { File.Move(temp, path, overwrite: true); }
            }
            else
            {
                File.Move(temp, path);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
