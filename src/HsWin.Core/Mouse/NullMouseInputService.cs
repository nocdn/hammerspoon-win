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

    public IDisposable Repeat(MouseButton button, MouseRepeatOptions options)
    {
        return new NullDisposable();
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
