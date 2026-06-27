using HsWin.Core.Alerts;

namespace HsWin.App;

internal static class CliInstallAlerts
{
    /// <summary>Shown until CLI install completes and replaces this toast.</summary>
    public const int InstallingDurationMs = 60_000;

    public const int InstalledDurationMs = 4500;

    public const int AlreadyInstalledDurationMs = 2500;

    public const int InstallFailedDurationMs = 7000;

    public static AlertRequest CreateInstallingAlert()
    {
        return AlertRequest.Create("Installing hspn CLI...", AlertKind.Normal, InstallingDurationMs, AlertIcon.Loader);
    }

    public static AlertRequest CreateInstalledAlert()
    {
        return AlertRequest.Create("hspn CLI installed. Open a new terminal to use it.", AlertKind.Success, InstalledDurationMs);
    }

    public static AlertRequest CreateAlreadyInstalledAlert()
    {
        return AlertRequest.Create("hspn CLI already installed.", AlertKind.Success, AlreadyInstalledDurationMs);
    }

    public static AlertRequest CreateInstallFailedAlert(Exception exception)
    {
        return AlertRequest.Create($"Could not install hspn CLI: {exception.Message}", AlertKind.Error, InstallFailedDurationMs);
    }
}
