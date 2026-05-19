using HsWin.Core.Logging;

namespace HsWin.Core.Scripting;

public sealed class ConsoleScriptApi
{
    private readonly IScriptConsoleLogger _console;

    public ConsoleScriptApi(IScriptConsoleLogger console)
    {
        _console = console;
    }

    public void Log(string level, string message)
    {
        _console.Write(level, message);
    }
}
