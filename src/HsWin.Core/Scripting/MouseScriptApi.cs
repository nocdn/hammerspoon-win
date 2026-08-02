using System.Globalization;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class MouseScriptApi
{
    private readonly IMouseService _mouse;
    private readonly IMouseInputService _mouseInput;
    private readonly IMouseEventService _mouseEvents;
    private readonly IRuntimeLogger _logger;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal MouseScriptApi(
        IMouseService mouse,
        IMouseInputService mouseInput,
        IMouseEventService mouseEvents,
        IRuntimeLogger logger,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _mouse = mouse;
        _mouseInput = mouseInput;
        _mouseEvents = mouseEvents;
        _logger = logger;
        _callbacks = callbacks;
        _trackResource = trackResource;
    }

    public string GetCurrentScreenJson()
    {
        var screen = _mouse.GetCurrentScreen();
        if (screen is null)
        {
            _logger.Info("Script hs.mouse.getCurrentScreen() returned null.");
            return "null";
        }

        _logger.Info($"Script hs.mouse.getCurrentScreen() returned id='{screen.Id}' name='{screen.Name}'.");
        return ScriptJson.Serialize(screen);
    }

    public bool IsOnPrimaryScreen()
    {
        var result = _mouse.GetCurrentScreen()?.IsPrimary == true;
        _logger.Info($"Script hs.mouse.isOnPrimaryScreen() returned {result}.");
        return result;
    }

    public void Click(object? button)
    {
        var parsedButton = MouseButtonParser.Parse(button);
        _mouseInput.Click(parsedButton);
        _logger.Info($"Script hs.mouse.click('{MouseButtonParser.GetDisplayName(parsedButton)}') requested.");
    }

    public MouseRepeatScriptHandle Repeat(object? button, object? options = null)
    {
        var parsedButton = MouseButtonParser.Parse(button);
        var parsedOptions = MouseScriptOptionsParser.ParseRepeatOptions(options);
        var handle = new MouseRepeatScriptHandle(_mouseInput.Repeat(parsedButton, parsedOptions));
        _trackResource(handle);
        _logger.Info(
            $"Script hs.mouse.repeat('{MouseButtonParser.GetDisplayName(parsedButton)}') intervalMs={parsedOptions.IntervalMs}.");
        return handle;
    }

    public void StopRepeat()
    {
        _mouseInput.StopActiveRepeat();
        _logger.Info("Script hs.mouse.stopRepeat() requested.");
    }

    public ScriptResourceHandle WatchScroll(object? callback, object? options = null)
    {
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Mouse scroll watch callback must be a JavaScript function.", nameof(callback));
        }

        var parsedOptions = MouseScriptOptionsParser.ParseScrollWatchOptions(options);
        var registration = _mouseEvents.WatchScroll(
            parsedOptions,
            scrollEvent =>
            {
                var eventJson = ScriptJson.Serialize(scrollEvent);
                var result = _callbacks.InvokeScriptCallback(scriptFunction, eventJson);
                // Return value is only meaningful for usage warnings on non-preventDefault watchers.
                // preventDefault swallow is decided natively on the hook path without JS.
                return Convert.ToBoolean(result, CultureInfo.InvariantCulture);
            });

        var handle = new ScriptResourceHandle(registration);
        _trackResource(handle);
        _logger.Info(
            $"Script hs.mouse.watchScroll() registered includeInjected={parsedOptions.IncludeInjected} " +
            $"blocking={parsedOptions.Blocking} axes={FormatAxes(parsedOptions.Axes)}.");
        return handle;
    }

    private static string FormatAxes(MouseScrollAxis axes)
    {
        return axes switch
        {
            MouseScrollAxis.Vertical => "vertical",
            MouseScrollAxis.Horizontal => "horizontal",
            MouseScrollAxis.Both => "both",
            _ => axes.ToString().ToLowerInvariant()
        };
    }
}
