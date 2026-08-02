namespace HsWin.Core.Mouse;

/// <summary>
/// A mouse wheel event delivered to script watchers. Positive <see cref="Delta"/> means
/// vertical scroll away from the user (up) or horizontal scroll to the right, matching
/// the Windows WM_MOUSEWHEEL / WM_MOUSEHWHEEL conventions.
/// </summary>
public sealed record MouseScrollEventSnapshot(
    string Type,
    string Axis,
    string Direction,
    int Delta,
    double Notches,
    bool IsVertical,
    bool IsHorizontal,
    bool IsInjected,
    string[] Modifiers,
    uint ModifierFlags,
    int X,
    int Y)
{
    public const string ScrollType = "scroll";

    public const string VerticalAxis = "vertical";

    public const string HorizontalAxis = "horizontal";

    public const string DirectionUp = "up";

    public const string DirectionDown = "down";

    public const string DirectionLeft = "left";

    public const string DirectionRight = "right";

    /// <summary>Windows WHEEL_DELTA: one notch is defined as 120 units.</summary>
    public const int WheelDelta = 120;
}
