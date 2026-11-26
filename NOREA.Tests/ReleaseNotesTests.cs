using NoreaLauncher;

namespace NOREA.Tests;

public class ReleaseNotesTests
{
    const string Sample = """
        # Changelog

        ## Non rilasciato

        - ancora in lavorazione

        ## v2.5.0

        - feat: add EverythingREA

        ## v2.4.1

        - fix(launcher): catalogo
        """;

    [Fact]
    public void LatestPair_SkipsUnreleasedAndUsesTheTwoNewestTags()
    {
        var pair = ReleaseNotes.LatestPair(Sample);

        Assert.Equal("v2.4.1", pair.Previous);
        Assert.Equal("v2.5.0", pair.Current);
        Assert.Contains("EverythingREA", pair.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("in lavorazione", pair.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ForVersion_FindsTheMatchingSection()
    {
        Assert.Contains("catalogo", ReleaseNotes.ForVersion(Sample, "V2.4.1"), StringComparison.Ordinal);
        Assert.Equal("", ReleaseNotes.ForVersion(Sample, "v9.9.9"));
        Assert.Equal("", ReleaseNotes.ForVersion(Sample, "Non rilasciato"));
    }

    [Fact]
    public void LatestPair_WithoutVersions_SaysNotesAreUnavailable()
    {
        var pair = ReleaseNotes.LatestPair("# Changelog\n\n## Non rilasciato\n\n- bozza\n");

        Assert.Equal("", pair.Previous);
        Assert.Equal("", pair.Current);
        Assert.Equal(ReleaseNotes.Unavailable, pair.Body);
    }
}
