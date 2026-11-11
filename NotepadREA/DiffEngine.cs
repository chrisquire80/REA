namespace NotepadREA;

public enum DiffLineType
{
    Unchanged,
    Inserted,
    Deleted,
    Modified
}

public sealed record DiffLine(DiffLineType Type, string? LeftText, string? RightText, int? LeftLineNumber, int? RightLineNumber);

public static class DiffEngine
{
    public static List<DiffLine> ComputeDiff(string leftText, string rightText)
    {
        var leftLines = leftText.Replace("\r\n", "\n").Split('\n');
        var rightLines = rightText.Replace("\r\n", "\n").Split('\n');

        int m = leftLines.Length;
        int n = rightLines.Length;

        // Matrice LCS (Longest Common Subsequence)
        int[,] lcs = new int[m + 1, n + 1];

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (leftLines[i] == rightLines[j])
                    lcs[i + 1, j + 1] = lcs[i, j] + 1;
                else
                    lcs[i + 1, j + 1] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var rawDiff = new List<(DiffLineType Type, string? Left, string? Right, int? LeftNum, int? RightNum)>();
        int x = m, y = n;

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && leftLines[x - 1] == rightLines[y - 1])
            {
                rawDiff.Add((DiffLineType.Unchanged, leftLines[x - 1], rightLines[y - 1], x, y));
                x--;
                y--;
            }
            else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
            {
                rawDiff.Add((DiffLineType.Inserted, null, rightLines[y - 1], null, y));
                y--;
            }
            else if (x > 0 && (y == 0 || lcs[x, y - 1] < lcs[x - 1, y]))
            {
                rawDiff.Add((DiffLineType.Deleted, leftLines[x - 1], null, x, null));
                x--;
            }
        }

        rawDiff.Reverse();

        // Passaggio per raggruppare Deleted + Inserted consecutivi in Modified
        var result = new List<DiffLine>();
        for (int i = 0; i < rawDiff.Count; i++)
        {
            if (i + 1 < rawDiff.Count &&
                rawDiff[i].Type == DiffLineType.Deleted &&
                rawDiff[i + 1].Type == DiffLineType.Inserted)
            {
                result.Add(new DiffLine(
                    DiffLineType.Modified,
                    rawDiff[i].Left,
                    rawDiff[i + 1].Right,
                    rawDiff[i].LeftNum,
                    rawDiff[i + 1].RightNum));
                i++; // salta il successivo
            }
            else
            {
                var r = rawDiff[i];
                result.Add(new DiffLine(r.Type, r.Left, r.Right, r.LeftNum, r.RightNum));
            }
        }

        return result;
    }
}
