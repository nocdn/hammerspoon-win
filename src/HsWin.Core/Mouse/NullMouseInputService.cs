namespace HsWin.Core.Mouse;

public sealed class NullMouseInputService : IMouseInputService
{
    public static NullMouseInputService Instance { get; } = new();

    private NullMouseInputService()
    {
    }

    public void Click(MouseButton button)
    {
    }

    public IMouseRepeatSession Repeat(MouseButton button, MouseRepeatOptions options)
    {
        return new NullRepeatSession();
    }

    public void StopActiveRepeat()
    {
    }

    private sealed class NullRepeatSession : IMouseRepeatSession
    {
        public int IntervalMs => MouseRepeatOptions.DefaultIntervalMs;

        public void SetIntervalMs(int intervalMs)
        {
        }

        public void Dispose()
        {
        }
    }
}
