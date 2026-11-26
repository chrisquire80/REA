namespace NoreaLauncher;

/// <summary>
/// Decisioni del catalogo e degli aggiornamenti che la finestra si limita a mostrare.
/// </summary>
public static class CatalogPolicy
{
    public static bool IsExcludedSuiteComponent(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        string lower = name.Trim().ToLowerInvariant();
        return lower is "norea" or "noreacli" or "norea-cli" or "norealauncher" or "noreamcp" or "noreadesign"
            || lower.EndsWith("tests") || lower.EndsWith(".tests")
            || lower.EndsWith(".core")
            || lower.Contains("benchmark") || lower.Contains("benchmarks");
    }

    public static bool ShouldShow(string id, bool planned, bool installed, bool hasDist, bool hasProject) =>
        !IsExcludedSuiteComponent(id) && !planned && (installed || hasDist || hasProject);

    public static bool HasDistUpdate(string version, DateTime distWriteUtc, DateTime installedWriteUtc) =>
        LauncherUtil.CompareVersions(version, "v0.0.0") >= 0 &&
        distWriteUtc > installedWriteUtc.AddSeconds(5);

    public static bool ShouldCheckCloudUpdates(bool manual, bool checkOnStartup) =>
        manual || checkOnStartup;
}
