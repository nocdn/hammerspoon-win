namespace HsWin.Core.Logging;

public interface IScriptConsoleLogger
{
    string? CurrentLogFilePath { get; }

    void BeginReload(string documentName);

    void Write(string level, string message);
}
