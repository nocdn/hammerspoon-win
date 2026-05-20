namespace HsWin.Core.Scripting;

public interface IScriptCallbackScheduler
{
    void Schedule(Action callback);
}
