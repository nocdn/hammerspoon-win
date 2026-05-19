namespace HammerspoonWin.Core.Applications;

public interface IApplicationProvider
{
    bool IsRunning(string processName);

    IReadOnlyList<ApplicationSnapshot> GetRunningApplications();
}
