namespace HsWin.Core.Shell;

public sealed record ShellExecutionOptions(
    string? WorkingDirectory,
    int TimeoutMs)
{
    public const int DefaultTimeoutMs = 30000;

    public static ShellExecutionOptions Default { get; } = new(null, DefaultTimeoutMs);
}
