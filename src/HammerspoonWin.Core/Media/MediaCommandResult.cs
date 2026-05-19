namespace HammerspoonWin.Core.Media;

public sealed record MediaCommandResult(
    string Command,
    bool Success,
    string Action,
    string StatusBefore,
    string StatusAfter,
    string Backend)
{
    public static MediaCommandResult Sent(string command, string backend)
    {
        return new MediaCommandResult(command, true, command, "unknown", "unknown", backend);
    }
}
