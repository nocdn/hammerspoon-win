namespace HsWin.Core.Logging;

public interface IRuntimeLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception exception);
}
