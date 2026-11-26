namespace EverythingREA;

/// <summary>
/// Opzioni e ordinamento che la finestra di ricerca applica prima di interrogare l'indice.
/// </summary>
public static class SearchSession
{
    public static SearchOptions CreateOptions(bool matchCase, bool matchPath, bool useRegex, QuickCategory category) =>
        new(matchCase, matchPath, useRegex, category);

    public static SortField ColumnToField(int column) => column switch
    {
        0 => SortField.Name,
        1 => SortField.Directory,
        2 => SortField.Size,
        3 => SortField.Modified,
        4 => SortField.Extension,
        _ => SortField.Name
    };

    public static (SortField Field, bool Descending) ToggleSort(SortField current, bool descending, int column)
    {
        SortField clicked = ColumnToField(column);
        if (current == clicked) return (clicked, !descending);
        return (clicked, false);
    }

    public static string FormatStatus(int shown, int total, long elapsedMs) =>
        $"{shown:N0} risultati su {total:N0} elementi · {elapsedMs} ms";

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024):0.##} MB";
        return $"{bytes / (1024d * 1024 * 1024):0.##} GB";
    }
}
