using NoreaLauncher;

namespace NOREA.Tests;

public class CatalogPolicyTests
{
    [Theory]
    [InlineData("NOREA.Tests", true)]
    [InlineData("EverythingREA.Core", true)]
    [InlineData("NOREA.Benchmarks", true)]
    [InlineData("NoreaCli", true)]
    [InlineData("NotepadREA", false)]
    [InlineData("WinSCPREA", false)]
    [InlineData("", true)]
    public void IsExcludedSuiteComponent_HidesLibrariesTestsAndTools(string id, bool excluded)
    {
        Assert.Equal(excluded, CatalogPolicy.IsExcludedSuiteComponent(id));
    }

    [Theory]
    [InlineData("NotepadREA", false, true, false, false, true)]
    [InlineData("NotepadREA", true, true, true, true, false)]
    [InlineData("NotepadREA", false, false, false, false, false)]
    [InlineData("NOREA.Tests", false, true, true, true, false)]
    public void ShouldShow_KeepsInstalledOrBuildableApps(string id, bool planned, bool installed, bool hasDist, bool hasProject, bool show)
    {
        Assert.Equal(show, CatalogPolicy.ShouldShow(id, planned, installed, hasDist, hasProject));
    }

    [Fact]
    public void HasDistUpdate_RequiresABuildNewerThanFiveSeconds()
    {
        var installed = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(CatalogPolicy.HasDistUpdate("v2.5.0", installed.AddSeconds(5), installed));
        Assert.True(CatalogPolicy.HasDistUpdate("v2.5.0", installed.AddSeconds(5).AddTicks(1), installed));
        Assert.False(CatalogPolicy.HasDistUpdate("", installed.AddMinutes(1), installed));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldCheckCloudUpdates_ManualOrStartup(bool manual, bool onStartup, bool check)
    {
        Assert.Equal(check, CatalogPolicy.ShouldCheckCloudUpdates(manual, onStartup));
    }
}
