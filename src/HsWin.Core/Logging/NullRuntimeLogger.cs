namespace HsWin.Core.Logging;

public sealed class NullRuntimeLogger : IRuntimeLogger
{
    public static NullRuntimeLogger Instance { get; } = new();

    private NullRuntimeLogger()
    {
    }

    public void Info(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string message, Exception exception)
    {
    }
}
