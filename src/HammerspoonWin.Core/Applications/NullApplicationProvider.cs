namespace HammerspoonWin.Core.Applications;

public sealed class NullApplicationProvider : IApplicationProvider
{
    public static NullApplicationProvider Instance { get; } = new();

    private NullApplicationProvider()
    {
    }

    public bool IsRunning(string processName)
    {
        return false;
    }

    public IReadOnlyList<ApplicationSnapshot> GetRunningApplications()
    {
        return [];
    }
}
