using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace NoreaCli;

public static class AiSetup
{
    public static (bool Success, string Message) RunSetup()
    {
        var logs = new List<string>();

        // 1. Registra nel PATH utente e installa norea.exe e norea.cmd
        try
        {
            var pathRes = EnsureInUserPath();
            logs.Add(pathRes);
        }
        catch (Exception ex)
        {
            logs.Add($"Avviso PATH: {ex.Message}");
        }

        // 2. Configura Claude Code
        try
        {
            var claudeRes = ConfigureClaudeCode();
            logs.Add(claudeRes);
        }
        catch (Exception ex)
        {
            logs.Add($"Avviso Claude Code: {ex.Message}");
        }

        // 3. Configura Antigravity
        try
        {
            var antiRes = ConfigureAntigravity();
            logs.Add(antiRes);
        }
        catch (Exception ex)
        {
            logs.Add($"Avviso Antigravity: {ex.Message}");
        }

        return (true, string.Join("\n", logs));
    }

    private static string EnsureInUserPath()
    {
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NOREA");
        Directory.CreateDirectory(targetDir);

        string currentExe = Environment.ProcessPath ?? AppContext.BaseDirectory;
        string targetExe = Path.Combine(targetDir, "norea.exe");
        string targetCmd = Path.Combine(targetDir, "norea.cmd");

        if (File.Exists(currentExe) && !string.Equals(currentExe, targetExe, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(currentExe, targetExe, overwrite: true);
        }

        // Script batch wrapper norea.cmd per CMD/PowerShell senza estensione
        File.WriteAllText(targetCmd, "@echo off\r\n\"%~dp0norea.exe\" %*\r\n");

        // Aggiorna PATH utente nel Registro
        using var key = Registry.CurrentUser.OpenSubKey(@"Environment", writable: true);
        if (key != null)
        {
            string currentPath = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
            var segments = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!segments.Any(s => string.Equals(s.Trim(), targetDir, StringComparison.OrdinalIgnoreCase)))
            {
                segments.Add(targetDir);
                key.SetValue("Path", string.Join(";", segments), RegistryValueKind.ExpandString);
            }
        }

        return $"✓ CLI NOREA installata in {targetDir} e registrata nel PATH utente.";
    }

    private static string ConfigureClaudeCode()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string claudeConfigDir = Path.Combine(userHome, ".claude");
        Directory.CreateDirectory(claudeConfigDir);

        string mcpConfigPath = Path.Combine(claudeConfigDir, "mcp.json");
        JsonObject root;

        if (File.Exists(mcpConfigPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(mcpConfigPath))?.AsObject() ?? [];
            }
            catch
            {
                root = [];
            }
        }
        else
        {
            root = [];
        }

        if (root["mcpServers"] is not JsonObject servers)
        {
            servers = [];
            root["mcpServers"] = servers;
        }

        servers["norea"] = new JsonObject
        {
            ["command"] = "norea",
            ["args"] = new JsonArray { "mcp" }
        };

        File.WriteAllText(mcpConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Aggiorna anche ~/.claude.json se presente
        string globalClaudeJson = Path.Combine(userHome, ".claude.json");
        if (File.Exists(globalClaudeJson))
        {
            try
            {
                var gRoot = JsonNode.Parse(File.ReadAllText(globalClaudeJson))?.AsObject();
                if (gRoot != null)
                {
                    if (gRoot["mcpServers"] is not JsonObject gServers)
                    {
                        gServers = [];
                        gRoot["mcpServers"] = gServers;
                    }
                    gServers["norea"] = new JsonObject
                    {
                        ["command"] = "norea",
                        ["args"] = new JsonArray { "mcp" }
                    };
                    File.WriteAllText(globalClaudeJson, gRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch { }
        }

        return $"✓ Server MCP NOREA configurato per Claude Code ({mcpConfigPath}).";
    }

    private static string ConfigureAntigravity()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string geminiDir = Path.Combine(userHome, ".gemini", "antigravity");
        string mcpDir = Path.Combine(geminiDir, "mcp", "norea");
        Directory.CreateDirectory(mcpDir);

        var configObj = new JsonObject
        {
            ["name"] = "norea",
            ["command"] = "norea",
            ["args"] = new JsonArray { "mcp" },
            ["description"] = "Suite NOREA (NotepadREA editor, visual side-by-side diff, hex inspection, desktop toast notifications)"
        };

        string configPath = Path.Combine(mcpDir, "server.json");
        File.WriteAllText(configPath, configObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        return $"✓ Server MCP NOREA configurato per Google Antigravity ({configPath}).";
    }
}
