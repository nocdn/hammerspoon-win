using HsWin.Core.Alerts;

namespace HsWin.App;

internal static class ConfigReloadAlerts
{
    /// <summary>Shown until reload completes and replaces this toast.</summary>
    public const int ReloadingDurationMs = 60_000;

    public const int ReloadedDurationMs = 3500;

    public const int ReloadFailedDurationMs = 10_000;

    public const int MinimumReloadingVisibleMs = 250;

    public static AlertRequest CreateReloadingAlert()
    {
        return AlertRequest.Create("Reloading config…", AlertKind.Normal, ReloadingDurationMs);
    }

    public static AlertRequest CreateReloadedAlert()
    {
        return AlertRequest.Create("Config reloaded", AlertKind.Success, ReloadedDurationMs);
    }

    public static AlertRequest CreateReloadFailedAlert(Exception exception)
    {
        var message = UserFacingExceptionFormatter.FormatConfigReloadFailure(exception);
        if (message.Contains("Hotkey already in use", StringComparison.OrdinalIgnoreCase))
        {
            message += " Edit config.js or close the conflicting app.";
        }
        else if (message.Contains("register hotkey", StringComparison.OrdinalIgnoreCase))
        {
            message += " Edit config.js or close the conflicting app.";
        }

        return AlertRequest.Create(message, AlertKind.Error, ReloadFailedDurationMs);
    }
}
