namespace HsWin.Core.Timers;

public interface IScriptTimerService
{
    IDisposable DoAfter(int delayMs, Action callback);

    IDisposable DoEvery(int intervalMs, Action callback);
}
