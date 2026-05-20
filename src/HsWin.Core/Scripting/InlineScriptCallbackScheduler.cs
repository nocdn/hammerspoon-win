namespace HsWin.Core.Scripting;

public sealed class InlineScriptCallbackScheduler : IScriptCallbackScheduler
{
    public static InlineScriptCallbackScheduler Instance { get; } = new();

    private InlineScriptCallbackScheduler()
    {
    }

    public void Schedule(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback();
    }
}
