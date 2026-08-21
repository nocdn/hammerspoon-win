namespace HsWin.Core.Applications;

public interface IApplicationProvider
{
    bool IsRunning(string processName);

    /// <param name="includeDetails">
    /// When false, snapshots carry only pid/processName and the provider skips per-process
    /// window-title and executable-path reads (the path read enumerates each process's modules
    /// and is expensive process-table wide).
    /// </param>
    IReadOnlyList<ApplicationSnapshot> GetRunningApplications(bool includeDetails);
}
