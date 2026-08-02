using System.Globalization;
using HsWin.Core.Keyboard;

namespace HsWin.Core.Scripting;

public sealed class KeyboardRepeatScriptHandle : ScriptResourceHandle
{
    private readonly IKeyboardRepeatSession _session;

    public KeyboardRepeatScriptHandle(IKeyboardRepeatSession session)
        : base(session)
    {
        _session = session;
    }

    public int IntervalMs => _session.IntervalMs;

    public void SetIntervalMs(object? intervalMs)
    {
        var value = Convert.ToInt32(intervalMs, CultureInfo.InvariantCulture);
        _session.SetIntervalMs(value);
    }

    public int getIntervalMs() => IntervalMs;

    public void setIntervalMs(object? intervalMs) => SetIntervalMs(intervalMs);
}
