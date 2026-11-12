using System.Text.Json.Nodes;
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
    public void AiSetup_GeneratesValidJsonStructures()
    {
        var (ok, msg) = AiSetup.RunSetup();
        Assert.True(ok);
        Assert.Contains("Server MCP NOREA configurato", msg);

        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string claudeMcp = Path.Combine(userHome, ".claude", "mcp.json");
        Assert.True(File.Exists(claudeMcp));

        var node = JsonNode.Parse(File.ReadAllText(claudeMcp));
        Assert.NotNull(node?["mcpServers"]?["norea"]);
        Assert.Equal("norea", node!["mcpServers"]!["norea"]!["command"]!.GetValue<string>());
    }
}
