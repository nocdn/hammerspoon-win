using System.Globalization;
using System.Text.Json;
using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class HsScriptHost
{
    private readonly IAlertPresenter _alerts;
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IScriptConsoleLogger _console;
    private readonly IApplicationProvider _applications;
    private readonly IMediaController _media;
    private readonly IRuntimeLogger _logger;
    private readonly Action<IDisposable> _trackResource;

    public HsScriptHost(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IRuntimeLogger logger,
        Action<IDisposable> trackResource)
    {
        _alerts = alerts;
        _hotkeys = hotkeys;
        _console = console;
        _applications = applications;
        _media = media;
        _logger = logger;
        _trackResource = trackResource;
    }

    public void ShowAlert(object? text, object? optionsOrKind = null, object? durationMs = null)
    {
        var request = AlertRequestParser.FromScriptArguments(text, optionsOrKind, durationMs);
        _alerts.Show(request);
    }

    public void LogConsole(string level, string message)
    {
        _console.Write(level, message);
    }

    public bool IsApplicationRunning(object? processName)
    {
        var normalizedProcessName = Convert.ToString(processName, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(normalizedProcessName))
        {
            throw new ArgumentException("Process name is required.", nameof(processName));
        }

        var result = _applications.IsRunning(normalizedProcessName);
        _logger.Info($"Script hs.application.isRunning('{normalizedProcessName}') returned {result}.");
        return result;
    }

    public string GetRunningApplicationsJson()
    {
        var applications = _applications.GetRunningApplications();
        _logger.Info($"Script hs.application.runningApplications() returned {applications.Count} processes.");
        return JsonSerializer.Serialize(applications, JsonOptions);
    }

    public string MediaPlayPauseJson()
    {
        _logger.Info("Script hs.media.playPause() requested.");
        var result = _media.PlayPause();
        _logger.Info($"Script hs.media.playPause() completed action='{result.Action}' statusBefore='{result.StatusBefore}' statusAfter='{result.StatusAfter}' success={result.Success} backend='{result.Backend}'.");
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    public string MediaPreviousTrackJson()
    {
        _logger.Info("Script hs.media.previousTrack() requested.");
        var result = _media.PreviousTrack();
        _logger.Info($"Script hs.media.previousTrack() completed success={result.Success} backend='{result.Backend}'.");
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    public string MediaNextTrackJson()
    {
        _logger.Info("Script hs.media.nextTrack() requested.");
        var result = _media.NextTrack();
        _logger.Info($"Script hs.media.nextTrack() completed success={result.Success} backend='{result.Backend}'.");
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    public IDisposable BindHotkey(object? modifiers, object? key, object? pressedFn)
    {
        if (pressedFn is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Hotkey callback must be a JavaScript function.", nameof(pressedFn));
        }

        var definition = HotkeyParser.Parse(modifiers, key);
        var registration = _hotkeys.Register(definition, () => InvokeHotkeyCallback(scriptFunction));
        _trackResource(registration);
        return registration;
    }

    private void InvokeHotkeyCallback(ScriptObject scriptFunction)
    {
        try
        {
            scriptFunction.Invoke(asConstructor: false);
        }
        catch (Exception exception)
        {
            _alerts.Show(AlertRequest.Create($"Hotkey callback error: {exception.Message}", AlertKind.Error, 7000));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
