namespace HsWin.Core.Mouse;

public interface IMouseInputService
{
    void Click(MouseButton button);

    IMouseRepeatSession Repeat(MouseButton button, MouseRepeatOptions options);

    /// <summary>
    /// Stops the active global mouse-repeat session, if any. Safe to call when idle.
    /// Use this as a belt-and-suspenders release path so a raced handle cannot keep clicking.
    /// </summary>
    void StopActiveRepeat();
}
