using System.Text.Json;
using NoreaCli;
using WinSCPREA;

namespace NOREA.Tests;

public class WinScpReaTests
{
    [Theory]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("/", "/")]
    [InlineData("var\\www//html/", "/var/www/html")]
    [InlineData("home/user/docs", "/home/user/docs")]
    public void NormalizeRemote_NormalizesSlashesAndRoot(string input, string expected)
    {
        Assert.Equal(expected, SftpSessionService.NormalizeRemote(input));
    }

    [Fact]
    public void CombineAndParentRemote_HandleRootAndNestedPaths()
    {
        Assert.Equal("/var", SftpSessionService.CombineRemote("/", "var"));
        Assert.Equal("/var/www/html", SftpSessionService.CombineRemote("/var/www/", "/html/"));
        Assert.Equal("/var/www", SftpSessionService.ParentRemote("/var/www/html"));
        Assert.Equal("/", SftpSessionService.ParentRemote("/var"));
        Assert.Equal("/", SftpSessionService.ParentRemote("/"));
    }

    [Fact]
    public void WinScpSettings_SaveAndLoad_RespectsEnvironmentOverride()
    {
        string root = Path.Combine(Path.GetTempPath(), $"winscp-settings-test-{Guid.NewGuid():N}");
        string settingsPath = Path.Combine(root, "settings.json");
        string? previous = Environment.GetEnvironmentVariable("WINSCPREA_SETTINGS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("WINSCPREA_SETTINGS_PATH", settingsPath);
            var settings = new WinScpSettings();
            settings.Sites.Add(new SiteProfile
            {
                Name = "prod",
                Host = "sftp.example.com",
                Port = 2222,
                Username = "deploy",
                RemoteDirectory = "/srv/app",
                Authentication = "Password"
            });
            settings.KnownHosts[WinScpSettings.HostKeyId("SFTP.EXAMPLE.COM ", 2222)] = "SHA256:abc123";
            settings.Save();

            var loaded = WinScpSettings.Load();
            Assert.Single(loaded.Sites);
            Assert.Equal("prod", loaded.Sites[0].Name);
            Assert.Equal(2222, loaded.Sites[0].Port);
            Assert.Equal("SHA256:abc123", loaded.KnownHosts["sftp.example.com:2222"]);

            File.WriteAllText(settingsPath, "{ invalid json");
            var fallback = WinScpSettings.Load();
            Assert.Empty(fallback.Sites);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WINSCPREA_SETTINGS_PATH", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WinScpAgentCli_SiteLifecycleAndStatus_WorksInIsolatedSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), $"winscp-cli-test-{Guid.NewGuid():N}");
        string settingsPath = Path.Combine(root, "settings.json");
        string? previous = Environment.GetEnvironmentVariable("WINSCPREA_SETTINGS_PATH");
        var originalOut = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("WINSCPREA_SETTINGS_PATH", settingsPath);

            using var addWriter = new StringWriter();
            Console.SetOut(addWriter);
            int addCode = await WinScpAgentCli.RunAsync(["site", "add", "staging", "--host", "10.0.0.5", "--username", "ci", "--port", "2200", "--json"]);
            Assert.Equal(0, addCode);

            using var listWriter = new StringWriter();
            Console.SetOut(listWriter);
            int listCode = await WinScpAgentCli.RunAsync(["site", "list", "--json"]);
            Assert.Equal(0, listCode);
            using var listDoc = JsonDocument.Parse(listWriter.ToString());
            Assert.True(listDoc.RootElement.GetProperty("ok").GetBoolean());

            using var statusWriter = new StringWriter();
            Console.SetOut(statusWriter);
            int statusCode = await WinScpAgentCli.RunAsync(["status", "--json"]);
            Assert.Equal(0, statusCode);
            using var statusDoc = JsonDocument.Parse(statusWriter.ToString());
            Assert.Equal(1, statusDoc.RootElement.GetProperty("data").GetProperty("siteCount").GetInt32());

            using var removeWriter = new StringWriter();
            Console.SetOut(removeWriter);
            int removeCode = await WinScpAgentCli.RunAsync(["site", "remove", "staging", "--yes", "--json"]);
            Assert.Equal(0, removeCode);
            Assert.Empty(WinScpSettings.Load().Sites);

            using var errWriter = new StringWriter();
            Console.SetOut(errWriter);
            int unknownCode = await WinScpAgentCli.RunAsync(["notacommand", "--json"]);
            Assert.NotEqual(0, unknownCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.SetEnvironmentVariable("WINSCPREA_SETTINGS_PATH", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
