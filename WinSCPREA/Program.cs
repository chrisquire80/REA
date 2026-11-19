namespace WinSCPREA;

internal static class Program
{
    private static int _exceptionReported;

    [STAThread]
    private static void Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportUnhandledException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                    ReportUnhandledException(exception);
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            ReportUnhandledException(exception);
        }
    }

    private static void ReportUnhandledException(Exception exception)
    {
        if (Interlocked.Exchange(ref _exceptionReported, 1) != 0) return;

        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NOREA", "WinSCPREA", "error.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }

        try
        {
            MessageBox.Show(
                $"WinSCPREA si è interrotto a causa di un errore.\n\n{exception.Message}\n\nDettagli registrati in:\n{logPath}",
                "Errore WinSCPREA", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
