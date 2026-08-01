namespace HsWin.Core.Mouse;

public interface IMouseInputService
{
    void Click(MouseButton button);

    IDisposable Repeat(MouseButton button, MouseRepeatOptions options);
}
