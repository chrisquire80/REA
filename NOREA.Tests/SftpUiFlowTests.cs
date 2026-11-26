using System.Text.Json;
using WinSCPREA;

namespace NOREA.Tests;

public class SftpUiFlowTests
{
    [Fact]
    public void Validate_RequiresHostAndUsername()
    {
        var result = SftpConnectionFlow.Validate("sito", "  ", 22, "", "/", false, "secret", "", "");

        Assert.False(result.Accepted);
        Assert.Equal("Dati mancanti", result.ErrorTitle);
        Assert.Null(result.Request);
    }

    [Fact]
    public void Validate_RequiresPasswordAndDoesNotStoreItOnTheSite()
    {
        var missing = SftpConnectionFlow.Validate("sito", "sftp.example", 22, "alice", "", false, "", "", "");
        Assert.False(missing.Accepted);
        Assert.Equal("Password richiesta", missing.ErrorTitle);

        var accepted = SftpConnectionFlow.Validate("  ", "sftp.example", 2222, "alice", "  ", false, "s3cret", "", "");
        Assert.True(accepted.Accepted);
        Assert.NotNull(accepted.Request);
        Assert.Equal("alice@sftp.example", accepted.Request.Site.Name);
        Assert.Equal("/", accepted.Request.Site.RemoteDirectory);
        Assert.Equal("Password", accepted.Request.Site.Authentication);
        Assert.Equal("", accepted.Request.Site.PrivateKeyPath);
        Assert.Equal("s3cret", accepted.Request.Password);

        string json = JsonSerializer.Serialize(accepted.Request.Site);
        Assert.DoesNotContain("s3cret", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PrivateKey_RequiresExistingFileAndKeepsPassphraseOffTheSite()
    {
        var missing = SftpConnectionFlow.Validate("k", "host", 22, "bob", "/data", true, "", Path.Combine(Path.GetTempPath(), "missing-key"), "phrase");
        Assert.False(missing.Accepted);
        Assert.Equal("Chiave non trovata", missing.ErrorTitle);

        string keyPath = Path.Combine(Path.GetTempPath(), $"norea-key-{Guid.NewGuid():N}");
        File.WriteAllText(keyPath, "not-a-real-key");
        try
        {
            var accepted = SftpConnectionFlow.Validate("Produzione", "host", 22, "bob", "/data", true, "ignored", keyPath, "phrase");

            Assert.True(accepted.Accepted);
            Assert.Equal("", accepted.Request!.Password);
            Assert.Equal("phrase", accepted.Request.KeyPassphrase);
            Assert.Equal("PrivateKey", accepted.Request.Site.Authentication);
            Assert.Equal(keyPath, accepted.Request.Site.PrivateKeyPath);
            Assert.DoesNotContain("phrase", JsonSerializer.Serialize(accepted.Request.Site), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    [Fact]
    public void UpsertSavedSite_UpdatesMatchingNameWithoutAddingASecondEntry()
    {
        var sites = new List<SiteProfile>
        {
            new() { Name = "Ufficio", Host = "old", Port = 22, Username = "a", RemoteDirectory = "/", Authentication = "Password" }
        };
        var updated = new SiteProfile
        {
            Name = "ufficio",
            Host = "new",
            Port = 2200,
            Username = "b",
            RemoteDirectory = "/inbox",
            Authentication = "PrivateKey",
            PrivateKeyPath = "C:\\keys\\id"
        };

        SftpConnectionFlow.UpsertSavedSite(sites, updated);

        Assert.Single(sites);
        Assert.Equal("Ufficio", sites[0].Name);
        Assert.Equal("new", sites[0].Host);
        Assert.Equal(2200, sites[0].Port);
        Assert.Equal("/inbox", sites[0].RemoteDirectory);
        Assert.Equal("C:\\keys\\id", sites[0].PrivateKeyPath);
    }

    [Fact]
    public void HostKey_TrustsKnownFingerprintAndPromptsOnFirstContactOrChange()
    {
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sftp.example:22"] = "abc"
        };

        var trusted = HostKeyTrust.Evaluate(known, "SFTP.EXAMPLE", 22, "abc");
        Assert.True(trusted.IsTrusted);
        Assert.Equal(HostKeyPromptKind.Trusted, trusted.Kind);

        var otherPort = HostKeyTrust.Evaluate(known, "sftp.example", 2222, "abc");
        Assert.Equal(HostKeyPromptKind.FirstContact, otherPort.Kind);
        Assert.Contains("SHA256:abc", HostKeyTrust.BuildPrompt("sftp.example", "abc", otherPort), StringComparison.Ordinal);

        var changed = HostKeyTrust.Evaluate(known, "sftp.example", 22, "def");
        Assert.Equal(HostKeyPromptKind.Changed, changed.Kind);
        string prompt = HostKeyTrust.BuildPrompt("sftp.example", "def", changed);
        Assert.Contains("SHA256:abc", prompt, StringComparison.Ordinal);
        Assert.Contains("SHA256:def", prompt, StringComparison.Ordinal);

        HostKeyTrust.Remember(known, "sftp.example", 2222, "abc");
        Assert.Equal("abc", known["sftp.example:2222"]);
    }
}
