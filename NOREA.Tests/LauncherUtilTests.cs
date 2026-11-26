using NoreaLauncher;
using Xunit;

namespace NOREA.Tests;

public class LauncherUtilTests
{
    [Theory]
    [InlineData("NotepadREA", "Notepad", "REA")]
    [InlineData("HexREA", "Hex", "REA")]
    [InlineData("SimpleApp", "SimpleApp", "")]
    [InlineData("", "", "")]
    public void SplitReaName_HandlesVariousPatterns(string input, string expectedPrefix, string expectedSuffix)
    {
        var (prefix, suffix) = LauncherUtil.SplitReaName(input);
        Assert.Equal(expectedPrefix, prefix);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Theory]
    [InlineData("v2.1.0", "v2.1.0")]
    [InlineData("V1.0", "v1.0")]
    [InlineData(" 3.0.4 ", "v3.0.4")]
    [InlineData(null, "v1.0.0")]
    [InlineData("", "v1.0.0")]
    public void NormalizeVersion_NormalizesToStandardVPrefix(string? input, string expected)
    {
        var result = LauncherUtil.NormalizeVersion(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("v1.5.0", "1.5.0", 0)]
    [InlineData("2.1.0", "2.0.9", 1)]
    [InlineData("v2.0.0.1", "v2.0.0", 1)]
    public void CompareVersions_ComparesSemantically(string v1, string v2, int expectedSign)
    {
        int result = LauncherUtil.CompareVersions(v1, v2);
        int sign = Math.Sign(result);
        Assert.Equal(expectedSign, sign);
    }

    [Fact]
    public void AnonymizePath_ReplacesKnownEnvironmentVariables()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var testPath = Path.Combine(localAppData, "Programs", "NOREA", "test.exe");
        var anonymized = LauncherUtil.AnonymizePath(testPath);

        Assert.StartsWith("%LocalAppData%", anonymized);
        Assert.DoesNotContain(localAppData, anonymized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeSha256_ComputesCorrectHash()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"
            var hash = LauncherUtil.ComputeFileSha256(tempFile);
            // SHA256 of "Hello" is 185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969
            Assert.Equal("185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969", hash);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetProcessTelemetry_CurrentProcess_ReturnsValidStats()
    {
        int currentPid = Environment.ProcessId;
        var telem = LauncherUtil.GetProcessTelemetry(currentPid);

        Assert.NotNull(telem);
        Assert.True(telem.Value.RamMb > 0);
        Assert.True(telem.Value.ThreadCount >= 1);
    }

    [Fact]
    public void GetProcessTelemetry_InvalidPid_ReturnsNull()
    {
        var telem = LauncherUtil.GetProcessTelemetry(-1);
        Assert.Null(telem);
    }

    [Fact]
    public void GetProcessTelemetry_OptOut_DoesNotReadTheProcess()
    {
        bool previous = ProcessTelemetryGate.Enabled;
        try
        {
            ProcessTelemetryGate.Enabled = false;
            Assert.Null(LauncherUtil.GetProcessTelemetry(Environment.ProcessId));

            ProcessTelemetryGate.Apply(new LauncherSettings { CollectProcessTelemetry = true });
            Assert.NotNull(LauncherUtil.GetProcessTelemetry(Environment.ProcessId));
        }
        finally
        {
            ProcessTelemetryGate.Enabled = previous;
        }
    }
}
