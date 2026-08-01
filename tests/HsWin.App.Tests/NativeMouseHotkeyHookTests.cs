using System.Runtime.InteropServices;
using HsWin.App.Hotkeys;
using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;

namespace HsWin.App.Tests;

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

    [Fact]
    public void RegisterInstallsHookOnDedicatedThread()
    {
        var platform = new FakeMouseHookPlatform();
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null);

        using var registration = hook.Register(
            HotkeyDefinition.CreateMouseButton(HotkeyModifiers.Control, HotkeyMouseButton.XButton1),
            () => { });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, platform.SetHookExCount);
        Assert.NotEqual(0u, platform.HookThreadId);
    }

    [Fact]
    public void LastRegistrationDisposeUnhooksAndStopsHookThread()
    {
        var platform = new FakeMouseHookPlatform();
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null);

        var registration = hook.Register(
            HotkeyDefinition.CreateMouseButton(HotkeyModifiers.Control, HotkeyMouseButton.XButton1),
            () => { });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        registration.Dispose();

        Assert.Equal(1, platform.UnhookCount);
        Assert.Equal(1, platform.QuitPostedCount);
    }

    [Fact]
    public void DispatchCallbackUsesCapturedSynchronizationContext()
    {
        var platform = new FakeMouseHookPlatform();
        var scheduler = new CapturingSynchronizationContext();
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, scheduler);

        using var registration = hook.Register(
            HotkeyDefinition.CreateMouseButton(HotkeyModifiers.None, HotkeyMouseButton.XButton1),
            () => { });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        platform.InvokeHook(0x020B, 0x0001u << 16);

        Assert.Equal(1, scheduler.PostCount);
    }

    [Fact]
    public void HeldRegistrationDispatchesPressAndReleaseCallbacks()
    {
        var platform = new FakeMouseHookPlatform();
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null);
        var pressed = 0;
        var released = 0;

        using var registration = hook.RegisterHeld(
            HotkeyDefinition.CreateMouseButton(HotkeyModifiers.None, HotkeyMouseButton.XButton1),
            () => pressed++,
            () => released++,
            blocking: true);

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(new IntPtr(1), platform.InvokeHook(0x020B, 0x0001u << 16));
        Assert.Equal(new IntPtr(1), platform.InvokeHook(0x020C, 0x0001u << 16));
        Assert.Equal(1, pressed);
        Assert.Equal(1, released);
    }

    [Fact]
    public void NonBlockingHeldRegistrationPassesMouseEventsThrough()
    {
        var platform = new FakeMouseHookPlatform();
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null);
        var pressed = 0;
        var released = 0;

        using var registration = hook.RegisterHeld(
            HotkeyDefinition.CreateMouseButton(HotkeyModifiers.None, HotkeyMouseButton.XButton1),
            () => pressed++,
            () => released++,
            blocking: false);

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(IntPtr.Zero, platform.InvokeHook(0x020B, 0x0001u << 16));
        Assert.Equal(IntPtr.Zero, platform.InvokeHook(0x020C, 0x0001u << 16));
        Assert.Equal(1, pressed);
        Assert.Equal(1, released);
    }

    private sealed class FakeMouseHookPlatform : IMouseHookPlatform
    {
        private readonly ManualResetEventSlim _quitSignal = new(false);
        private MouseHookProcedure? _hookProcedure;

        public int SetHookExCount { get; private set; }

        public int UnhookCount { get; private set; }

        public int QuitPostedCount { get; private set; }

        public uint HookThreadId { get; private set; }

        public ManualResetEventSlim InstallCompleted { get; } = new(false);

        public IntPtr SetWindowsHookEx(int idHook, MouseHookProcedure hookProcedure, IntPtr moduleHandle, uint threadId)
        {
            SetHookExCount++;
            _hookProcedure = hookProcedure;
            InstallCompleted.Set();
            return new IntPtr(0x1234);
        }

        public bool UnhookWindowsHookEx(IntPtr hookHandle)
        {
            UnhookCount++;
            return true;
        }

        public IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam) =>
            IntPtr.Zero;

        public short GetAsyncKeyState(int virtualKey) => 0;

        public int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax)
        {
            _quitSignal.Wait(TimeSpan.FromSeconds(5));
            message = default;
            return 0;
        }

        public bool TranslateMessage(ref NativeMessage message) => true;

        public IntPtr DispatchMessage(ref NativeMessage message) => IntPtr.Zero;

        public bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam)
        {
            if (message == 0x0012)
            {
                QuitPostedCount++;
                _quitSignal.Set();
            }

            return true;
        }

        public uint GetCurrentThreadId()
        {
            HookThreadId = (uint)Environment.CurrentManagedThreadId;
            return HookThreadId;
        }

        public IntPtr GetModuleHandle(string? moduleName) => new IntPtr(1);

        public IntPtr InvokeHook(int message, uint mouseData)
        {
            if (_hookProcedure is null)
            {
                throw new InvalidOperationException("Hook procedure was not installed.");
            }

            var hookData = new MouseHookData
            {
                MouseData = mouseData
            };

            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<MouseHookData>());
            try
            {
                Marshal.StructureToPtr(hookData, pointer, false);
                return _hookProcedure(0, new IntPtr(message), pointer);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookData
        {
            public int X;

            public int Y;

            public uint MouseData;

            public uint Flags;

            public uint Time;

            public UIntPtr ExtraInfo;
        }
    }

    private sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCount++;
            d(state);
        }
    }
}
