using NoreaDesign;

namespace NoreaLauncher;

public static class Log
{
    static readonly NoreaLog Inner = new("NOREA", "launcher");

    public static void Info(string message) => Inner.Info(message);
    public static void Warn(string message, Exception? ex = null) => Inner.Warn(message, ex);
    public static void Error(string message, Exception? ex = null) => Inner.Error(message, ex);
}
