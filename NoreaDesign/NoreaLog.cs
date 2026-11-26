using System.Text;

namespace NoreaDesign;

/// <summary>
/// Log su file condiviso. Ogni app sceglie cartella e prefisso, così i percorsi già usati non cambiano.
/// </summary>
public sealed class NoreaLog
{
    readonly object _sync = new();
    readonly string _directory;
    readonly string _filePrefix;

    public NoreaLog(string appFolderName, string filePrefix)
    {
        _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appFolderName, "logs");
        _filePrefix = filePrefix;
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    void Write(string level, string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            string file = Path.Combine(_directory, $"{_filePrefix}-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
            if (ex != null) sb.AppendLine().Append(ex.ToString());
            sb.AppendLine();

            lock (_sync)
            {
                File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { }
    }
}
