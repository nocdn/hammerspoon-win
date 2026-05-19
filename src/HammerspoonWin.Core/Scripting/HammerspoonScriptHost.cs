using HammerspoonWin.Core.Alerts;
using HammerspoonWin.Core.Hotkeys;
using HammerspoonWin.Core.Logging;
using Microsoft.ClearScript;

namespace HammerspoonWin.Core.Scripting;

public sealed class HammerspoonScriptHost
{
    private readonly IAlertPresenter _alerts;
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IScriptConsoleLogger _console;
    private readonly Action<IDisposable> _trackResource;

    public HammerspoonScriptHost(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        Action<IDisposable> trackResource)
    {
        _alerts = alerts;
        _hotkeys = hotkeys;
        _console = console;
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
}
