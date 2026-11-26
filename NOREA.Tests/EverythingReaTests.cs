using EverythingREA;

namespace NOREA.Tests;

public class EverythingReaTests
{
    [Fact]
    public async Task FileIndexService_IndexesAndFiltersWithEverythingSyntax()
    {
        string root = Path.Combine(Path.GetTempPath(), $"everything-rea-test-{Guid.NewGuid():N}");
        string subDocs = Path.Combine(root, "docs");
        string subCode = Path.Combine(root, "src");
        string excludedDir = Path.Combine(root, "node_modules");
        Directory.CreateDirectory(subDocs);
        Directory.CreateDirectory(subCode);
        Directory.CreateDirectory(excludedDir);

        File.WriteAllText(Path.Combine(subDocs, "report_2026.txt"), new string('a', 2048));
        File.WriteAllText(Path.Combine(subDocs, "draft_notes.md"), "hello");
        File.WriteAllText(Path.Combine(subCode, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(excludedDir, "ignored.js"), "console.log(1);");

        try
        {
            var settings = new EverythingSettings
            {
                IndexedRoots = [root],
                WatchFileSystem = false
            };

            using var index = new FileIndexService();
            int total = await index.RebuildIndexAsync(settings);

            Assert.True(total >= 5);
            Assert.Empty(index.Search("ignored.js"));

            // AND + negation
            var docsNoDraft = index.Search("report !draft");
            Assert.Single(docsNoDraft);
            Assert.Equal("report_2026.txt", docsNoDraft[0].Name);

            // OR operator
            var orResults = index.Search("Program.cs | draft_notes.md");
            Assert.Equal(2, orResults.Count);

            // ext: filter
            var csAndMd = index.Search("ext:cs;md");
            Assert.Equal(2, csAndMd.Count);

            // folder: and file: + size:>
            var folders = index.Search("folder:");
            Assert.Equal(2, folders.Count);

            var largeFiles = index.Search("file: size:>1kb");
            Assert.Single(largeFiles);
            Assert.Equal("report_2026.txt", largeFiles[0].Name);

            // Wildcard and Regex
            Assert.Single(index.Search("report_*.txt"));
            Assert.Single(index.Search(@"regex:^Program\.cs$"));

            // MatchPath
            var byPath = index.Search("src", new SearchOptions(MatchPath: true));
            Assert.Contains(byPath, e => e.Name == "Program.cs");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EverythingSettingsAndCache_PersistInIsolatedPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), $"everything-rea-cache-{Guid.NewGuid():N}");
        string dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "sample.json"), "{}");

        string settingsPath = Path.Combine(root, "settings.json");
        string cachePath = Path.Combine(root, "index.json");
        string? prevSettings = Environment.GetEnvironmentVariable("EVERYTHINGREA_SETTINGS_PATH");
        string? prevCache = Environment.GetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH");

        try
        {
            Environment.SetEnvironmentVariable("EVERYTHINGREA_SETTINGS_PATH", settingsPath);
            Environment.SetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH", cachePath);

            var settings = new EverythingSettings
            {
                IndexedRoots = [dataDir],
                WatchFileSystem = false
            };
            settings.Save();

            var loadedSettings = EverythingSettings.Load();
            Assert.Single(loadedSettings.IndexedRoots);

            using var index1 = new FileIndexService();
            await index1.RebuildIndexAsync(loadedSettings);
            index1.SaveCache();

            using var index2 = new FileIndexService();
            Assert.True(index2.LoadCache());
            Assert.Single(index2.Search("sample.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("EVERYTHINGREA_SETTINGS_PATH", prevSettings);
            Environment.SetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH", prevCache);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Search_NullCacheFields_DoesNotThrowFromCompileQuery()
    {
        string root = Path.Combine(Path.GetTempPath(), $"everything-null-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string cachePath = Path.Combine(root, "index.json");
        File.WriteAllText(cachePath, """
            [
              null,
              {"Name":null,"Directory":null,"FullPath":null,"IsDirectory":false,"SizeBytes":1,"ModifiedUtc":"2026-01-01T00:00:00Z","Extension":null},
              {"Name":null,"Directory":null,"FullPath":"C:\\data\\ok.txt","IsDirectory":false,"SizeBytes":4,"ModifiedUtc":"2026-01-01T00:00:00Z","Extension":null}
            ]
            """);
        string? previous = Environment.GetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH");
        try
        {
            Environment.SetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH", cachePath);
            using var index = new FileIndexService();
            Assert.True(index.LoadCache());

            Assert.Contains(index.Search("ok", sortBy: SortField.Extension), entry => entry.Name == "ok.txt");
            Assert.Contains(index.Search("!missing"), entry => entry.Name == "ok.txt");
            Assert.Contains(index.Search("file:"), entry => entry.Name == "ok.txt");
            Assert.Contains(index.Search("*.txt"), entry => entry.Name == "ok.txt");
            Assert.Contains(index.Search("size:>0"), entry => entry.Name == "ok.txt");
            Assert.Contains(index.Search("regex:ok"), entry => entry.Name == "ok.txt");
            Assert.Empty(index.Search("ext:txt"));

            var code = index.Search("", new SearchOptions(Category: QuickCategory.Code));
            Assert.DoesNotContain(code, entry => entry.Name == "ok.txt");
        }
        finally
        {
            Environment.SetEnvironmentVariable("EVERYTHINGREA_CACHE_PATH", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_DuringRebuild_DoesNotThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), $"everything-race-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        for (int i = 0; i < 40; i++)
            File.WriteAllText(Path.Combine(root, $"file-{i:D2}.txt"), "x");

        try
        {
            using var index = new FileIndexService();
            var settings = new EverythingSettings { IndexedRoots = [root], WatchFileSystem = false };
            var searching = Task.Run(() =>
            {
                for (int i = 0; i < 30; i++)
                {
                    index.Search("file !missing ext:txt *.txt size:>0");
                    index.Search("regex:file", new SearchOptions(UseRegex: true));
                }
            });
            await index.RebuildIndexAsync(settings);
            await searching;
            Assert.Contains(index.Search("file-01"), entry => entry.Name == "file-01.txt");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
