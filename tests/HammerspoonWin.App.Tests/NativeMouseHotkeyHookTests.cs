using HammerspoonWin.App.Hotkeys;
using HammerspoonWin.Core.Hotkeys;

namespace HammerspoonWin.App.Tests;

public sealed class NativeMouseHotkeyHookTests
{
    [Fact]
    public void TryGetMouseButtonEventDecodesMiddleButtonDownAndUp()
    {
        Assert.True(NativeMouseHotkeyHook.TryGetMouseButtonEvent(0x0207, 0, out var down));
        Assert.Equal(HotkeyMouseButton.Middle, down.Button);
        Assert.True(down.IsDown);

        Assert.True(NativeMouseHotkeyHook.TryGetMouseButtonEvent(0x0208, 0, out var up));
        Assert.Equal(HotkeyMouseButton.Middle, up.Button);
        Assert.False(up.IsDown);
    }

    [Theory]
    [InlineData(0x0001u, HotkeyMouseButton.XButton1)]
    [InlineData(0x0002u, HotkeyMouseButton.XButton2)]
    public void TryGetMouseButtonEventDecodesXButtonDown(uint xButton, HotkeyMouseButton expectedButton)
    {
        var mouseData = xButton << 16;

        Assert.True(NativeMouseHotkeyHook.TryGetMouseButtonEvent(0x020B, mouseData, out var buttonEvent));
        Assert.Equal(expectedButton, buttonEvent.Button);
        Assert.True(buttonEvent.IsDown);
    }

    [Fact]
    public void TryGetMouseButtonEventRejectsUnknownXButton()
    {
        var mouseData = 0x0003u << 16;

        Assert.False(NativeMouseHotkeyHook.TryGetMouseButtonEvent(0x020B, mouseData, out _));
    }
}
