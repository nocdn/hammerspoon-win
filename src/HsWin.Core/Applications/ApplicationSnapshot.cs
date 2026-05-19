namespace HsWin.Core.Applications;

public sealed record ApplicationSnapshot(
    int Pid,
    string ProcessName,
    string? MainWindowTitle,
    string? Path);
