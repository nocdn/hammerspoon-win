namespace HsWin.Core.Mouse;

[Flags]
public enum MouseScrollAxis
{
    None = 0,
    Vertical = 1,
    Horizontal = 2,
    Both = Vertical | Horizontal
}
