using System.Text;

namespace NotepadREA;

public static class Log
{
    static readonly object Sync = new();
    static string LogDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotepadREA", "logs");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var dir = LogDir;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"notepadrea-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
            if (ex != null) sb.AppendLine().Append(ex.ToString());
            sb.AppendLine();

            lock (Sync)
            {
                File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { }
    }
}
