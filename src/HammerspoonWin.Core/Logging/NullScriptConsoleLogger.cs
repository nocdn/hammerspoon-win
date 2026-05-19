namespace HammerspoonWin.Core.Logging;

public sealed class NullScriptConsoleLogger : IScriptConsoleLogger
{
    public static NullScriptConsoleLogger Instance { get; } = new();

    private NullScriptConsoleLogger()
    {
    }

    public string? CurrentLogFilePath => null;

    public void BeginReload(string documentName)
    {
    }

    public void Write(string level, string message)
    {
    }
}
