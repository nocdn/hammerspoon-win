using System.Globalization;
using HsWin.Core.Mouse;

namespace HsWin.Core.Scripting;

public sealed class MouseRepeatScriptHandle : ScriptResourceHandle
{
    private readonly IMouseRepeatSession _session;

    public MouseRepeatScriptHandle(IMouseRepeatSession session)
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

    // ClearScript / JS-friendly casing aliases.
    public int getIntervalMs() => IntervalMs;

    public void setIntervalMs(object? intervalMs) => SetIntervalMs(intervalMs);
}
