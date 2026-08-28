namespace HsWin.App;

using System.Windows;

/// <summary>
/// Windows equivalents of Snap's cursor-status pill tokens. Values are in
/// device-independent pixels so the pill scales with the active display.
/// </summary>
internal static class FollowingToastStyleMetrics
{
    public static readonly FontWeight TextFontWeight = FontWeights.Medium;

    public const double TextFontSize = 12;
    public const double TextMaxWidth = 520;
    public const byte FillColorComponent = 251;
    public const byte OutlineColorComponent = 229;
    public const byte TextColorComponent = 51;
    public const double HorizontalPadding = 8;
    public const double VerticalPadding = 4;
    public const double OutlineWidth = 1;
    public const double CursorOffsetX = 16;
    public const double CursorOffsetY = 18;
    public const double ScreenInset = 12;
    public const double FollowResponseMs = 60;
    public const double TargetFrameRate = 240;
    public const double MinimumFrameIntervalMs = 1000d / 240d;
    public const double MaximumFrameIntervalMs = 1000d / 30d;
    public const double ShadowBlurRadius = 2;
    public const double ShadowOpacity = 0.10;
    public const double ShadowInset = 4;
    public const int EnterDurationMs = 120;
    public const int ExitDurationMs = 160;
    public const double TransitionBlurRadius = 0.15;
}
