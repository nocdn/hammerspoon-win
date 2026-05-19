namespace HsWin.Core.Shell;

public sealed record ShellExecutionResult(
    string Command,
    bool Success,
    int? ExitCode,
    string Output,
    string Error,
    bool TimedOut)
{
    public bool Status => Success;
}
