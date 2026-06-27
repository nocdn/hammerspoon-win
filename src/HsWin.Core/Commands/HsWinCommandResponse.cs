namespace HsWin.Core.Commands;

public sealed record HsWinCommandResponse(bool Success, string Message)
{
    public static HsWinCommandResponse Ok(string message) => new(true, message);

    public static HsWinCommandResponse Error(string message) => new(false, message);
}
