using HsWin.Core.Alerts;
using HsWin.Core.Logging;
using Microsoft.ClearScript;
using System.Diagnostics;

namespace HsWin.Core.Scripting;

internal sealed class ScriptCallbackInvoker
{
    private readonly IAlertPresenter _alerts;
    private readonly IRuntimeLogger _logger;

    public ScriptCallbackInvoker(IAlertPresenter alerts, IRuntimeLogger logger)
    {
        _alerts = alerts;
        _logger = logger;
    }

    public void InvokeHotkeyCallback(ScriptObject scriptFunction)
    {
        var startedAt = Stopwatch.GetTimestamp();
        _logger.Info("Script hotkey callback started.");
        try
        {
            scriptFunction.Invoke(asConstructor: false);
            _logger.Info($"Script hotkey callback completed elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
        }
        catch (Exception exception)
        {
            _logger.Warning($"Script hotkey callback failed elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}. {exception.Message}");
            _alerts.Show(AlertRequest.Create($"Hotkey callback error: {exception.Message}", AlertKind.Error, 7000));
        }
    }

    public object? InvokeScriptCallback(ScriptObject scriptFunction, params object?[] args)
    {
        var startedAt = Stopwatch.GetTimestamp();
        _logger.Info($"Script callback started args={args.Length}.");
        try
        {
            var result = scriptFunction.Invoke(asConstructor: false, args);
            _logger.Info($"Script callback completed args={args.Length} elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
            return result;
        }
        catch (Exception exception)
        {
            _logger.Warning($"Script callback failed args={args.Length} elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}. {exception.Message}");
            _alerts.Show(AlertRequest.Create($"Callback error: {exception.Message}", AlertKind.Error, 7000));
            return false;
        }
    }
}
