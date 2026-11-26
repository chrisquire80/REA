namespace NoreaLauncher;

public sealed record ReleaseNoteSection(string Version, string Body);

/// <summary>
/// Note di rilascio lette da CHANGELOG.md (generato dai commit fra i tag).
/// </summary>
public static class ReleaseNotes
{
    public const string Unavailable = "Note di rilascio non disponibili.";

    public static string Load()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "CHANGELOG.md");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
            }
        }

        return "";
    }

    public static IReadOnlyList<ReleaseNoteSection> Parse(string? markdown)
    {
        var sections = new List<ReleaseNoteSection>();
        if (string.IsNullOrWhiteSpace(markdown)) return sections;

        string? version = null;
        var body = new System.Text.StringBuilder();
        void Flush()
        {
            if (version is null) return;
            sections.Add(new ReleaseNoteSection(version, body.ToString().Trim()));
            body.Clear();
        }

        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                version = raw[3..].Trim();
            }
            else if (version is not null)
            {
                body.AppendLine(raw);
            }
        }

        Flush();
        return sections;
    }

    public static bool IsReleasedVersion(string heading) =>
        heading.Length > 1 &&
        (heading[0] == 'v' || heading[0] == 'V') &&
        char.IsDigit(heading[1]);

    public static (string Previous, string Current, string Body) LatestPair(string? markdown)
    {
        var released = Parse(markdown).Where(section => IsReleasedVersion(section.Version)).ToList();
        if (released.Count == 0) return ("", "", Unavailable);
        if (released.Count == 1) return ("", released[0].Version, string.IsNullOrWhiteSpace(released[0].Body) ? Unavailable : released[0].Body);
        string body = string.IsNullOrWhiteSpace(released[0].Body) ? Unavailable : released[0].Body;
        return (released[1].Version, released[0].Version, body);
    }

    public static string ForVersion(string? markdown, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "";
        string wanted = LauncherUtil.NormalizeVersion(version);
        var match = Parse(markdown).FirstOrDefault(section =>
            IsReleasedVersion(section.Version) &&
            LauncherUtil.NormalizeVersion(section.Version) == wanted);
        return match?.Body ?? "";
    }
}
