namespace HsWin.Core.Shell;

public sealed record LaunchOptions(
    string? WorkingDirectory,
    string? Arguments)
{
    public static LaunchOptions Default { get; } = new(null, null);
}
