using HsWin.Core.Logging;
using HsWin.Core.Timers;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class TimerScriptApi
{
    private readonly IScriptTimerService _timers;
    private readonly IRuntimeLogger _logger;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal TimerScriptApi(
        IScriptTimerService timers,
        IRuntimeLogger logger,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _timers = timers;
        _logger = logger;
        _callbacks = callbacks;
        _trackResource = trackResource;
    }

    public ScriptResourceHandle DoAfter(object? delayMs, object? callback)
    {
        var delay = ConvertTimerInterval(delayMs, nameof(delayMs));
        return CreateTimerHandle(_timers.DoAfter(delay, () => InvokeTimerCallback(callback)), $"doAfter {delay}ms");
    }

    public ScriptResourceHandle DoEvery(object? intervalMs, object? callback)
    {
        var interval = ConvertTimerInterval(intervalMs, nameof(intervalMs));
        return CreateTimerHandle(_timers.DoEvery(interval, () => InvokeTimerCallback(callback)), $"doEvery {interval}ms");
    }

    private void InvokeTimerCallback(object? callback)
    {
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Timer callback must be a JavaScript function.", nameof(callback));
        }

        _callbacks.InvokeScriptCallback(scriptFunction);
    }

    private ScriptResourceHandle CreateTimerHandle(IDisposable timer, string description)
    {
        var handle = new ScriptResourceHandle(timer);
        _trackResource(handle);
        _logger.Info($"Script hs.timer.{description} created.");
        return handle;
    }

    private static int ConvertTimerInterval(object? value, string argumentName)
    {
        var interval = ScriptArgumentReader.RequireInt32(value, argumentName, "a number of milliseconds");
        if (interval < 1)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Timer interval must be at least 1 millisecond.");
        }

        return interval;
    }
}
