namespace HsWin.Core.Shell;

public sealed record LaunchResult(
    string Target,
    bool Success,
    int? ProcessId,
    string? Error);
