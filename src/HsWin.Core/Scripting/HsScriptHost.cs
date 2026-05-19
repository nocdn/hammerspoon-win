using System.Globalization;
using System.Text.Json;
using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Timers;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class HsScriptHost
{
    private readonly IAlertPresenter _alerts;
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IScriptConsoleLogger _console;
    private readonly IApplicationProvider _applications;
    private readonly IMediaController _media;
    private readonly IKeyboardEventService _keyboardEvents;
    private readonly IKeyboardInputService _keyboardInput;
    private readonly IScriptTimerService _timers;
    private readonly IRuntimeLogger _logger;
    private readonly Action<IDisposable> _trackResource;

    public HsScriptHost(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IKeyboardEventService keyboardEvents,
        IKeyboardInputService keyboardInput,
        IScriptTimerService timers,
        IRuntimeLogger logger,
        Action<IDisposable> trackResource)
    {
        _alerts = alerts;
        _hotkeys = hotkeys;
        _console = console;
        _applications = applications;
        _media = media;
        _keyboardEvents = keyboardEvents;
        _keyboardInput = keyboardInput;
        _timers = timers;
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

    public ScriptResourceHandle WatchKeyboard(object? callback, object? options = null)
    {
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Keyboard watch callback must be a JavaScript function.", nameof(callback));
        }

        var parsedOptions = KeyboardScriptOptionsParser.ParseWatchOptions(options);
        var registration = _keyboardEvents.Watch(
            parsedOptions,
            keyboardEvent =>
            {
                var eventJson = JsonSerializer.Serialize(keyboardEvent, JsonOptions);
                var result = InvokeScriptCallback(scriptFunction, eventJson);
                return Convert.ToBoolean(result, CultureInfo.InvariantCulture);
            });

        var handle = new ScriptResourceHandle(registration);
        _trackResource(handle);
        _logger.Info($"Script hs.keyboard.watch() registered includeInjected={parsedOptions.IncludeInjected}.");
        return handle;
    }

    public void KeyboardTap(object? key, object? options = null)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        var parsedOptions = KeyboardScriptOptionsParser.ParseTapOptions(options);
        _keyboardInput.Tap(virtualKey, parsedOptions);
    }

    public ScriptResourceHandle KeyboardRepeat(object? key, object? options = null)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        var parsedOptions = KeyboardScriptOptionsParser.ParseRepeatOptions(options);
        var handle = new ScriptResourceHandle(_keyboardInput.Repeat(virtualKey, parsedOptions));
        _trackResource(handle);
        _logger.Info(
            $"Script hs.keyboard.repeat('{KeyboardKeyRules.GetDisplayName(virtualKey)}') intervalMs={parsedOptions.IntervalMs} suppressModifiers=0x{(uint)parsedOptions.SuppressPhysicalModifiers:X}.");
        return handle;
    }

    public void KeyboardKeyDown(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        _keyboardInput.KeyDown(virtualKey);
        _logger.Info($"Script hs.keyboard.keyDown('{KeyboardKeyRules.GetDisplayName(virtualKey)}') requested.");
    }

    public void KeyboardKeyUp(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        _keyboardInput.KeyUp(virtualKey);
        _logger.Info($"Script hs.keyboard.keyUp('{KeyboardKeyRules.GetDisplayName(virtualKey)}') requested.");
    }

    public bool KeyboardIsDown(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        return _keyboardEvents.IsKeyDown(virtualKey);
    }

    public ScriptResourceHandle TimerDoAfter(object? delayMs, object? callback)
    {
        var delay = ConvertTimerInterval(delayMs, nameof(delayMs));
        var handle = CreateTimerHandle(_timers.DoAfter(delay, () => InvokeTimerCallback(callback)), $"doAfter {delay}ms");
        return handle;
    }

    public ScriptResourceHandle TimerDoEvery(object? intervalMs, object? callback)
    {
        var interval = ConvertTimerInterval(intervalMs, nameof(intervalMs));
        var handle = CreateTimerHandle(_timers.DoEvery(interval, () => InvokeTimerCallback(callback)), $"doEvery {interval}ms");
        return handle;
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

    private object? InvokeScriptCallback(ScriptObject scriptFunction, params object?[] args)
    {
        try
        {
            return scriptFunction.Invoke(asConstructor: false, args);
        }
        catch (Exception exception)
        {
            _alerts.Show(AlertRequest.Create($"Callback error: {exception.Message}", AlertKind.Error, 7000));
            return false;
        }
    }

    private void InvokeTimerCallback(object? callback)
    {
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Timer callback must be a JavaScript function.", nameof(callback));
        }

        InvokeScriptCallback(scriptFunction);
    }

    private ScriptResourceHandle CreateTimerHandle(IDisposable timer, string description)
    {
        var handle = new ScriptResourceHandle(timer);
        _trackResource(handle);
        _logger.Info($"Script hs.timer.{description} created.");
        return handle;
    }

    private static int ConvertTimerInterval(object? value, string parameterName)
    {
        if (value is null || ReferenceEquals(value, Undefined.Value))
        {
            throw new ArgumentException("Timer interval is required.", parameterName);
        }

        try
        {
            var interval = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (interval < 1)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Timer interval must be at least 1 millisecond.");
            }

            return interval;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Timer interval must be a number of milliseconds.", parameterName, exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException("Timer interval must be a number of milliseconds.", parameterName, exception);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
