using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EverythingREA;

public sealed class FileIndexService : IDisposable
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".slnx", ".sln", ".py", ".js", ".ts", ".tsx", ".jsx", ".json", ".xml", ".yaml", ".yml",
        ".c", ".cpp", ".h", ".hpp", ".rs", ".go", ".java", ".kt", ".ps1", ".sh", ".sql", ".html", ".css", ".md"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".rtf", ".odt", ".ods"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"
    };

    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private List<FileIndexEntry> _entries = [];
    private Dictionary<string, int> _pathToIndex = new(StringComparer.OrdinalIgnoreCase);

    public event Action? IndexUpdated;

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public DateTime LastIndexedUtc { get; private set; }

    public async Task<int> RebuildIndexAsync(EverythingSettings settings, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var roots = EverythingSettings.NormalizeRoots(settings.IndexedRoots);
        var excluded = new HashSet<string>(settings.ExcludedFolderNames ?? [], StringComparer.OrdinalIgnoreCase);

        var collected = await Task.Run(() =>
        {
            var bag = new ConcurrentBag<FileIndexEntry>();
            int total = 0;

            foreach (string root in roots)
            {
                ct.ThrowIfCancellationRequested();
                ScanDirectoryTree(root, excluded, settings.IncludeHidden, bag, ref total, progress, ct);
            }

            return bag.Where(static entry => entry is not null)
                      .OrderBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase)
                      .ThenBy(e => e.FullPath ?? "", StringComparer.OrdinalIgnoreCase)
                      .ToList();
        }, ct);

        lock (_gate)
        {
            _entries = collected;
            RebuildLookupUnsafe();
            LastIndexedUtc = DateTime.UtcNow;
        }

        ConfigureWatchers(settings, roots, excluded);
        IndexUpdated?.Invoke();
        return collected.Count;
    }

    public bool LoadCache()
    {
        try
        {
            string cachePath = EverythingSettings.IndexCachePath;
            if (!File.Exists(cachePath)) return false;
            var entries = JsonSerializer.Deserialize<List<FileIndexEntry>>(File.ReadAllText(cachePath));
            if (entries == null || entries.Count == 0) return false;
            var normalized = new List<FileIndexEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (NormalizeEntry(entry) is { } safe)
                    normalized.Add(safe);
            }
            if (normalized.Count == 0) return false;
            lock (_gate)
            {
                _entries = normalized;
                RebuildLookupUnsafe();
                LastIndexedUtc = File.GetLastWriteTimeUtc(cachePath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SaveCache()
    {
        try
        {
            List<FileIndexEntry> snapshot;
            lock (_gate) snapshot = [.. _entries];
            string cachePath = EverythingSettings.IndexCachePath;
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            string tmp = cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            if (File.Exists(cachePath))
                File.Replace(tmp, cachePath, null);
            else
                File.Move(tmp, cachePath);
        }
        catch { }
    }

    public IReadOnlyList<FileIndexEntry> Search(
        string? query,
        SearchOptions? options = null,
        SortField sortBy = SortField.Name,
        bool descending = false)
    {
        options ??= new SearchOptions();
        // Copia sotto il lock: i watcher aggiornano la stessa lista mentre la ricerca
        // la enumera. Una lettura concorrente può consegnare un elemento null al
        // predicato e far esplodere CompileQuery con NullReferenceException.
        FileIndexEntry[] snapshot;
        lock (_gate) snapshot = _entries.Where(static entry => entry is not null).ToArray();

        IEnumerable<FileIndexEntry> filtered = snapshot;

        if (options.Category != QuickCategory.All)
            filtered = filtered.Where(e => MatchesCategory(e, options.Category));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var matcher = CompileQuery(query.Trim(), options);
            filtered = filtered.Where(matcher);
        }

        IOrderedEnumerable<FileIndexEntry> sorted = (sortBy, descending) switch
        {
            (SortField.Name, false) => filtered.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Name, true) => filtered.OrderByDescending(e => e.IsDirectory).ThenByDescending(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Directory, false) => filtered.OrderBy(e => e.Directory ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Directory, true) => filtered.OrderByDescending(e => e.Directory ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Size, false) => filtered.OrderBy(e => e.IsDirectory ? -1L : e.SizeBytes).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Size, true) => filtered.OrderByDescending(e => e.IsDirectory ? -1L : e.SizeBytes).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Modified, false) => filtered.OrderBy(e => e.ModifiedUtc),
            (SortField.Modified, true) => filtered.OrderByDescending(e => e.ModifiedUtc),
            (SortField.Extension, false) => filtered.OrderBy(e => e.Extension ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            (SortField.Extension, true) => filtered.OrderByDescending(e => e.Extension ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase)
        };

        return sorted.Take(Math.Max(1, options.MaxResults)).ToList();
    }

    private static void ScanDirectoryTree(
        string root,
        HashSet<string> excluded,
        bool includeHidden,
        ConcurrentBag<FileIndexEntry> bag,
        ref int total,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var pending = new ConcurrentStack<string>();
        pending.Push(root);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8),
            CancellationToken = ct
        };

        var enumOpts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = includeHidden
                ? FileAttributes.ReparsePoint
                : FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
        };

        while (!pending.IsEmpty)
        {
            var batch = new List<string>(128);
            while (batch.Count < 128 && pending.TryPop(out var next))
                batch.Add(next);

            int localCount = 0;
            Parallel.ForEach(batch, parallelOptions, dir =>
            {
                try
                {
                    foreach (var info in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", enumOpts))
                    {
                        if (ct.IsCancellationRequested) break;

                        if (info is DirectoryInfo subDir)
                        {
                            if (excluded.Contains(subDir.Name)) continue;
                            bag.Add(new FileIndexEntry(
                                subDir.Name,
                                dir,
                                subDir.FullName,
                                true,
                                0,
                                subDir.LastWriteTimeUtc,
                                ""));
                            pending.Push(subDir.FullName);
                        }
                        else if (info is FileInfo file)
                        {
                            bag.Add(new FileIndexEntry(
                                file.Name,
                                dir,
                                file.FullName,
                                false,
                                file.Length,
                                file.LastWriteTimeUtc,
                                file.Extension));
                        }

                        int added = Interlocked.Increment(ref localCount);
                        if ((added & 0x3FF) == 0)
                            progress?.Report(bag.Count);
                    }
                }
                catch
                {
                }
            });

            Interlocked.Add(ref total, localCount);
            progress?.Report(bag.Count);
        }
    }

    private void ConfigureWatchers(EverythingSettings settings, List<string> roots, HashSet<string> excluded)
    {
        DisposeWatchers();
        if (!settings.WatchFileSystem) return;

        foreach (string root in roots)
        {
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Created += (_, e) => OnFsChanged(e.FullPath, deleted: false, excluded);
                watcher.Changed += (_, e) => OnFsChanged(e.FullPath, deleted: false, excluded);
                watcher.Deleted += (_, e) => OnFsChanged(e.FullPath, deleted: true, excluded);
                watcher.Renamed += (_, e) =>
                {
                    OnFsChanged(e.OldFullPath, deleted: true, excluded);
                    OnFsChanged(e.FullPath, deleted: false, excluded);
                };
                _watchers.Add(watcher);
            }
            catch { }
        }
    }

    private void OnFsChanged(string fullPath, bool deleted, HashSet<string> excluded)
    {
        try
        {
            if (IsPathExcluded(fullPath, excluded)) return;

            lock (_gate)
            {
                if (deleted)
                {
                    RemoveEntryUnsafe(fullPath);
                }
                else
                {
                    var entry = TryCreateEntry(fullPath);
                    if (entry != null)
                        UpsertEntryUnsafe(entry);
                }
            }
            IndexUpdated?.Invoke();
        }
        catch { }
    }

    private static bool IsPathExcluded(string fullPath, HashSet<string> excluded)
    {
        var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => excluded.Contains(p));
    }

    private static FileIndexEntry? TryCreateEntry(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            var di = new DirectoryInfo(fullPath);
            return new FileIndexEntry(di.Name, di.Parent?.FullName ?? "", di.FullName, true, 0, di.LastWriteTimeUtc, "");
        }
        if (File.Exists(fullPath))
        {
            var fi = new FileInfo(fullPath);
            return new FileIndexEntry(fi.Name, fi.DirectoryName ?? "", fi.FullName, false, fi.Length, fi.LastWriteTimeUtc, fi.Extension);
        }
        return null;
    }

    private void RebuildLookupUnsafe()
    {
        _pathToIndex = new Dictionary<string, int>(_entries.Count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _entries.Count; i++)
            _pathToIndex[_entries[i].FullPath] = i;
    }

    private void UpsertEntryUnsafe(FileIndexEntry entry)
    {
        if (_pathToIndex.TryGetValue(entry.FullPath, out int idx))
            _entries[idx] = entry;
        else
        {
            _pathToIndex[entry.FullPath] = _entries.Count;
            _entries.Add(entry);
        }
    }

    private void RemoveEntryUnsafe(string fullPath)
    {
        if (!_pathToIndex.TryGetValue(fullPath, out int idx)) return;
        int last = _entries.Count - 1;
        if (idx != last)
        {
            var moved = _entries[last];
            _entries[idx] = moved;
            _pathToIndex[moved.FullPath] = idx;
        }
        _entries.RemoveAt(last);
        _pathToIndex.Remove(fullPath);
    }

    private static bool MatchesCategory(FileIndexEntry entry, QuickCategory category) => category switch
    {
        QuickCategory.Folders => entry.IsDirectory,
        QuickCategory.Files => !entry.IsDirectory,
        QuickCategory.Code => !entry.IsDirectory && HasListedExtension(entry, CodeExtensions),
        QuickCategory.Documents => !entry.IsDirectory && HasListedExtension(entry, DocumentExtensions),
        QuickCategory.Images => !entry.IsDirectory && HasListedExtension(entry, ImageExtensions),
        QuickCategory.Archives => !entry.IsDirectory && HasListedExtension(entry, ArchiveExtensions),
        _ => true
    };

    private static bool HasListedExtension(FileIndexEntry entry, HashSet<string> extensions) =>
        entry.Extension is { Length: > 0 } extension && extensions.Contains(extension);

    private static FileIndexEntry? NormalizeEntry(FileIndexEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FullPath)) return null;
        return entry with
        {
            Name = string.IsNullOrEmpty(entry.Name) ? FileNameOf(entry.FullPath) : entry.Name,
            Directory = entry.Directory ?? DirectoryOf(entry.FullPath),
            Extension = entry.Extension ?? ""
        };
    }

    private static string FileNameOf(string fullPath)
    {
        int slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash >= 0 && slash < fullPath.Length - 1 ? fullPath[(slash + 1)..] : fullPath;
    }

    private static string DirectoryOf(string fullPath)
    {
        int slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash > 0 ? fullPath[..slash] : "";
    }

    private static string MatchText(string? value) => value ?? "";

    private static Func<FileIndexEntry, bool> CompileQuery(string query, SearchOptions options)
    {
        StringComparison cmp = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        RegexOptions rxOpts = RegexOptions.Compiled | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);

        if (options.UseRegex || query.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            string pattern = query.StartsWith("regex:", StringComparison.OrdinalIgnoreCase) ? query[6..] : query;
            try
            {
                var rx = new Regex(pattern, rxOpts);
                return entry => entry is not null && rx.IsMatch(MatchText(options.MatchPath ? entry.FullPath : entry.Name));
            }
            catch
            {
                return _ => false;
            }
        }

        // Split by OR branches ("|"), each branch requires all space-separated tokens (AND)
        var orBranches = query.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var compiledBranches = new List<List<Func<FileIndexEntry, bool>>>();

        foreach (string branch in orBranches)
        {
            var tokens = Tokenize(branch);
            var predicates = new List<Func<FileIndexEntry, bool>>();
            foreach (string rawToken in tokens)
            {
                bool negated = rawToken.StartsWith('!') && rawToken.Length > 1;
                string token = negated ? rawToken[1..] : rawToken;
                var pred = BuildTokenPredicate(token, options.MatchPath, cmp, rxOpts);
                predicates.Add(negated ? e => !pred(e) : pred);
            }
            if (predicates.Count > 0)
                compiledBranches.Add(predicates);
        }

        if (compiledBranches.Count == 0)
            return _ => true;

        return entry => entry is not null && compiledBranches.Any(branch => branch.All(p => p(entry)));
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char ch in input)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    private static Func<FileIndexEntry, bool> BuildTokenPredicate(
        string token,
        bool matchPath,
        StringComparison cmp,
        RegexOptions rxOpts)
    {
        if (token.Equals("folder:", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("dir:", StringComparison.OrdinalIgnoreCase))
            return e => e is not null && e.IsDirectory;

        if (token.Equals("file:", StringComparison.OrdinalIgnoreCase))
            return e => e is not null && !e.IsDirectory;

        if (token.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
        {
            var exts = token[4..]
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.StartsWith('.') ? x : "." + x)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return e => e is not null && !e.IsDirectory && HasListedExtension(e, exts);
        }

        if (token.StartsWith("size:", StringComparison.OrdinalIgnoreCase))
        {
            string expr = token[5..].Trim();
            if (TryParseSizeFilter(expr, out bool greaterThan, out long thresholdBytes))
                return e => e is not null && !e.IsDirectory && (greaterThan ? e.SizeBytes >= thresholdBytes : e.SizeBytes <= thresholdBytes);
        }

        if (token.Contains('*') || token.Contains('?'))
        {
            string pattern = "^" + Regex.Escape(token).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var rx = new Regex(pattern, rxOpts);
            return e => e is not null && rx.IsMatch(MatchText(matchPath ? e.FullPath : e.Name));
        }

        return e => e is not null && MatchText(matchPath ? e.FullPath : e.Name).Contains(token, cmp);
    }

    private static bool TryParseSizeFilter(string expr, out bool greaterThan, out long bytes)
    {
        greaterThan = true;
        bytes = 0;
        if (string.IsNullOrWhiteSpace(expr)) return false;

        if (expr.StartsWith(">=")) { greaterThan = true; expr = expr[2..]; }
        else if (expr.StartsWith("<=")) { greaterThan = false; expr = expr[2..]; }
        else if (expr.StartsWith('>')) { greaterThan = true; expr = expr[1..]; }
        else if (expr.StartsWith('<')) { greaterThan = false; expr = expr[1..]; }

        expr = expr.Trim().ToLowerInvariant();
        long multiplier = 1;
        if (expr.EndsWith("gb")) { multiplier = 1024L * 1024 * 1024; expr = expr[..^2]; }
        else if (expr.EndsWith("mb")) { multiplier = 1024L * 1024; expr = expr[..^2]; }
        else if (expr.EndsWith("kb")) { multiplier = 1024L; expr = expr[..^2]; }
        else if (expr.EndsWith("b")) { expr = expr[..^1]; }

        if (double.TryParse(expr.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            bytes = (long)(value * multiplier);
            return true;
        }
        return false;
    }

    private void DisposeWatchers()
    {
        foreach (var w in _watchers)
        {
            try { w.Dispose(); } catch { }
        }
        _watchers.Clear();
    }

    public void Dispose() => DisposeWatchers();
}
