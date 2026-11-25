namespace EverythingREA;

internal static class Program
{
    private static int _exceptionReported;

    [STAThread]
    private static void Main(string[] args)
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

            string initialQuery = string.Join(" ", args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)));
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(initialQuery));
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
            "NOREA", "EverythingREA", "error.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }

        MessageBox.Show(
            $"Si è verificato un errore inatteso in EverythingREA:\n\n{exception.Message}",
            "EverythingREA",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
