using System.Text.Json.Nodes;
using System.Text;
using NoreaCli;

namespace NOREA.Tests;

public class McpAndCliTests
{
    [Fact]
    public void NoreaBridge_DetectWorkspaceDir_FindsWorkspace()
    {
        string ws = NoreaBridge.DetectWorkspaceDir();
        Assert.False(string.IsNullOrWhiteSpace(ws));
        Assert.True(Directory.Exists(ws));
    }

    [Fact]
    public void NoreaBridge_GetAppsStatus_ReturnsApps()
    {
        var apps = NoreaBridge.GetAppsStatus(runningOnly: false);
        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a["name"].ToString() == "NotepadREA");
        Assert.Contains(apps, a => a["name"].ToString() == "NoreaLauncher");
    }

    [Fact]
    public void AiSetup_GeneratesValidJsonStructuresWithoutTouchingUserProfile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-ai-setup-test-{Guid.NewGuid():N}");
        string installDir = Path.Combine(root, "install");
        try
        {
            var options = new AiSetupOptions(
                UserHome: root,
                InstallDirectory: installDir,
                CurrentExecutable: null,
                RegisterUserPath: false,
                InstallCli: false);

            var (ok, msg) = AiSetup.RunSetup(options);
            Assert.True(ok, msg);
            Assert.Contains("Server MCP NOREA configurato", msg);

            string claudeMcp = Path.Combine(root, ".claude", "mcp.json");
            string antigravity = Path.Combine(root, ".gemini", "antigravity", "mcp", "norea", "server.json");
            var claudeNode = JsonNode.Parse(File.ReadAllText(claudeMcp));
            var antigravityNode = JsonNode.Parse(File.ReadAllText(antigravity));
            Assert.Equal("norea", claudeNode?["mcpServers"]?["norea"]?["command"]?.GetValue<string>());
            Assert.Equal("mcp", claudeNode?["mcpServers"]?["norea"]?["args"]?[0]?.GetValue<string>());
            Assert.Equal("norea", antigravityNode?["name"]?.GetValue<string>());
            Assert.False(Directory.Exists(installDir));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AiSetup_PreservesInvalidExistingConfiguration()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-ai-setup-test-{Guid.NewGuid():N}");
        string configDir = Path.Combine(root, ".claude");
        Directory.CreateDirectory(configDir);
        string configPath = Path.Combine(configDir, "mcp.json");
        const string invalidJson = "{ invalid";
        File.WriteAllText(configPath, invalidJson);
        try
        {
            var options = new AiSetupOptions(root, Path.Combine(root, "install"), null,
                RegisterUserPath: false, InstallCli: false);
            var (success, message) = AiSetup.RunSetup(options);

            Assert.False(success);
            Assert.Contains("preservato", message);
            Assert.Equal(invalidJson, File.ReadAllText(configPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpServer_ProcessesInitializeToolListAndToolErrorsOverStdio()
    {
        string inputText = string.Join("\n",
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"not_a_tool","arguments":{}}}""");
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(inputText));
        await using var output = new MemoryStream();

        await McpServer.RunAsync(input, output);

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8);
        var responses = (await reader.ReadToEndAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line))
            .ToArray();

        Assert.Equal(3, responses.Length);
        Assert.Equal("2024-11-05", responses[0]?["result"]?["protocolVersion"]?.GetValue<string>());
        Assert.Contains(responses[1]?["result"]?["tools"]?.AsArray() ?? [],
            tool => tool?["name"]?.GetValue<string>() == "norea_open_file");
        Assert.True(responses[2]?["result"]?["isError"]?.GetValue<bool>());
    }

    [Fact]
    public async Task EverythingSearchCli_IndexesRootAndPrintsMatches()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-search-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "report_2026.txt"), "fattura");
        File.WriteAllText(Path.Combine(root, "notes.md"), "bozza");
        try
        {
            var missing = new StringWriter();
            Assert.Equal(1, await EverythingSearchCli.RunAsync([], new StringWriter(), missing));
            Assert.Contains("query", missing.ToString(), StringComparison.OrdinalIgnoreCase);

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = await EverythingSearchCli.RunAsync(
                ["report", "--root", root, "--limit", "10"], stdout, stderr);

            Assert.Equal(0, code);
            Assert.Contains("report_2026.txt", stdout.ToString());
            Assert.DoesNotContain("notes.md", stdout.ToString());

            var jsonOut = new StringWriter();
            Assert.Equal(0, await EverythingSearchCli.RunAsync(
                ["ext:md", "--root", root, "--json"], jsonOut, new StringWriter()));
            Assert.Contains("notes.md", jsonOut.ToString());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
