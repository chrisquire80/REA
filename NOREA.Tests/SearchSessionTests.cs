using EverythingREA;

namespace NOREA.Tests;

public class SearchSessionTests
{
    [Fact]
    public void CreateOptions_PassesTheFlagsTheWindowCollects()
    {
        var options = SearchSession.CreateOptions(true, true, false, QuickCategory.Code);

        Assert.True(options.MatchCase);
        Assert.True(options.MatchPath);
        Assert.False(options.UseRegex);
        Assert.Equal(QuickCategory.Code, options.Category);
    }

    [Fact]
    public void ToggleSort_FlipsTheSameColumnAndResetsANewOne()
    {
        var (field, descending) = SearchSession.ToggleSort(SortField.Name, false, 0);
        Assert.Equal(SortField.Name, field);
        Assert.True(descending);

        (field, descending) = SearchSession.ToggleSort(SortField.Name, true, 2);
        Assert.Equal(SortField.Size, field);
        Assert.False(descending);
        Assert.Equal(SortField.Extension, SearchSession.ColumnToField(4));
        Assert.Equal(SortField.Name, SearchSession.ColumnToField(99));
    }

    [Fact]
    public void FormatStatusAndSize_UseTheSameTextAsTheResultList()
    {
        Assert.Equal($"{1024:N0} risultati su {2000:N0} elementi · 12 ms", SearchSession.FormatStatus(1024, 2000, 12));
        Assert.Equal("512 B", SearchSession.FormatSize(512));
        Assert.Equal($"{1536 / 1024d:0.#} KB", SearchSession.FormatSize(1536));
    }

    [Fact]
    public async Task QuotedPhraseInvalidRegexAndSort_FollowTheWindowQuery()
    {
        string root = Path.Combine(Path.GetTempPath(), $"everything-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "alpha report.txt"), "a");
        File.WriteAllText(Path.Combine(root, "beta.txt"), new string('b', 2048));
        try
        {
            using var index = new FileIndexService();
            await index.RebuildIndexAsync(new EverythingSettings { IndexedRoots = [root], WatchFileSystem = false });

            var quoted = index.Search("\"alpha report\"", SearchSession.CreateOptions(false, false, false, QuickCategory.All));
            Assert.Single(quoted);
            Assert.Equal("alpha report.txt", quoted[0].Name);

            var broken = index.Search("regex:(", SearchSession.CreateOptions(false, false, false, QuickCategory.All));
            Assert.Empty(broken);

            var bySize = index.Search("", SearchSession.CreateOptions(false, false, false, QuickCategory.Files), SortField.Size, descending: true);
            Assert.Equal("beta.txt", bySize[0].Name);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
