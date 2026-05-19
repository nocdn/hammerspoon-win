using HsWin.Core.Alerts;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

internal sealed class ScriptCallbackInvoker
{
    private readonly IAlertPresenter _alerts;

    public ScriptCallbackInvoker(IAlertPresenter alerts)
    {
        _alerts = alerts;
    }

    public void InvokeHotkeyCallback(ScriptObject scriptFunction)
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

    public object? InvokeScriptCallback(ScriptObject scriptFunction, params object?[] args)
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
}
