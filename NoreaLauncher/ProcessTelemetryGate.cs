namespace NoreaLauncher;

/// <summary>
/// Interruttore delle metriche di processo. Il valore arriva dalle impostazioni;
/// <see cref="LauncherUtil.GetProcessTelemetry"/> non legge il processo se è spento.
/// </summary>
public static class ProcessTelemetryGate
{
    public static bool Enabled { get; set; } = true;

    public static void Apply(LauncherSettings settings) => Enabled = settings.CollectProcessTelemetry;
}
