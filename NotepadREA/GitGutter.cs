using System.Diagnostics;
using System.Text.RegularExpressions;
using ScintillaNET;

namespace NotepadREA;

public enum GitLineStatus
{
    Added,
    Modified,
    Deleted
}

public sealed record GitDiffHunk(int Line, GitLineStatus Status, int Count);

public static class GitGutter
{
    public const int GitMargin = 4;
    public const int MarkerAdded = 24;
    public const int MarkerModified = 25;
    public const int MarkerDeleted = 26;

    private static readonly Regex HunkRegex = new(@"^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@", RegexOptions.Compiled);

    public static void ConfigureEditor(Scintilla sci, Theme theme)
    {
        var m = sci.Margins[GitMargin];
        m.Type = MarginType.Symbol;
        m.Width = 4;
        m.Sensitive = false;
        m.Mask = (1u << MarkerAdded) | (1u << MarkerModified) | (1u << MarkerDeleted);

        // Marker Added: barra verde piena
        var mAdd = sci.Markers[MarkerAdded];
        mAdd.Symbol = MarkerSymbol.FullRect;
        mAdd.SetBackColor(Color.FromArgb(55, 175, 80));

        // Marker Modified: barra blu/ciano piena
        var mMod = sci.Markers[MarkerModified];
        mMod.Symbol = MarkerSymbol.FullRect;
        mMod.SetBackColor(Color.FromArgb(50, 145, 235));

        // Marker Deleted: barra/triangolo rosso
        var mDel = sci.Markers[MarkerDeleted];
        mDel.Symbol = MarkerSymbol.FullRect;
        mDel.SetBackColor(Color.FromArgb(235, 60, 60));
    }

    public static List<GitDiffHunk> ParseDiffOutput(string diffOutput)
    {
        var hunks = new List<GitDiffHunk>();
        if (string.IsNullOrWhiteSpace(diffOutput)) return hunks;

        using var reader = new StringReader(diffOutput);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var match = HunkRegex.Match(line);
            if (!match.Success) continue;

            int oldStart = int.Parse(match.Groups[1].Value);
            int oldCount = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
            int newStart = int.Parse(match.Groups[3].Value);
            int newCount = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;

            // Scintilla usa indici riga 0-based
            int zeroLine = Math.Max(0, newStart - 1);

            if (oldCount == 0 && newCount > 0)
            {
                hunks.Add(new GitDiffHunk(zeroLine, GitLineStatus.Added, newCount));
            }
            else if (newCount == 0 && oldCount > 0)
            {
                hunks.Add(new GitDiffHunk(zeroLine, GitLineStatus.Deleted, 1));
            }
            else
            {
                hunks.Add(new GitDiffHunk(zeroLine, GitLineStatus.Modified, Math.Max(1, newCount)));
            }
        }

        return hunks;
    }

    public static async Task UpdateGutterAsync(Scintilla sci, string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            ClearGutter(sci);
            return;
        }

        string? dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        string fileName = Path.GetFileName(filePath);

        try
        {
            var hunks = await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("git", $"diff --no-ext-diff -U0 --no-color -- \"{fileName}\"")
                    {
                        WorkingDirectory = dir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return [];
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(1500);
                    return ParseDiffOutput(output);
                }
                catch { return []; }
            });

            if (sci.IsDisposed) return;

            sci.MarkerDeleteAll(MarkerAdded);
            sci.MarkerDeleteAll(MarkerModified);
            sci.MarkerDeleteAll(MarkerDeleted);

            foreach (var h in hunks)
            {
                int marker = h.Status switch
                {
                    GitLineStatus.Added => MarkerAdded,
                    GitLineStatus.Modified => MarkerModified,
                    _ => MarkerDeleted
                };

                for (int i = 0; i < h.Count; i++)
                {
                    int lineIdx = h.Line + i;
                    if (lineIdx < sci.Lines.Count)
                    {
                        sci.Lines[lineIdx].MarkerAdd(marker);
                    }
                }
            }
        }
        catch { }
    }

    public static void ClearGutter(Scintilla sci)
    {
        if (sci.IsDisposed) return;
        sci.MarkerDeleteAll(MarkerAdded);
        sci.MarkerDeleteAll(MarkerModified);
        sci.MarkerDeleteAll(MarkerDeleted);
    }
}
