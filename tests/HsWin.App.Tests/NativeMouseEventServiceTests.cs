using System.Runtime.InteropServices;
using HsWin.App.Hotkeys;
using HsWin.App.Mouse;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Tests;

public sealed class NativeMouseEventServiceTests
{
    [Theory]
    [InlineData(0x020A, 120, true, "up")]
    [InlineData(0x020A, -120, true, "down")]
    [InlineData(0x020E, 120, false, "right")]
    [InlineData(0x020E, -120, false, "left")]
    public void TryCreateScrollEventDecodesWheelMessages(int message, int delta, bool vertical, string direction)
    {
        var mouseData = unchecked((uint)((ushort)delta << 16));

        Assert.True(NativeMouseEventService.TryCreateScrollEvent(
            message,
            mouseData,
            flags: 0,
            x: 12,
            y: 34,
            pressedModifiers: default,
            out var snapshot));

        Assert.Equal(MouseScrollEventSnapshot.ScrollType, snapshot.Type);
        Assert.Equal(vertical, snapshot.IsVertical);
        Assert.Equal(!vertical, snapshot.IsHorizontal);
        Assert.Equal(direction, snapshot.Direction);
        Assert.Equal(delta, snapshot.Delta);
        Assert.Equal(delta / 120.0, snapshot.Notches);
        Assert.Equal(12, snapshot.X);
        Assert.Equal(34, snapshot.Y);
    }

    [Fact]
    public void TryCreateScrollEventMarksInjectedFlag()
    {
        var mouseData = 120u << 16;

        Assert.True(NativeMouseEventService.TryCreateScrollEvent(
            0x020A,
            mouseData,
            flags: 0x00000001,
            x: 0,
            y: 0,
            pressedModifiers: default,
            out var snapshot));

        Assert.True(snapshot.IsInjected);
    }

    [Fact]
    public void TryCreateScrollEventRejectsNonWheelMessages()
    {
        Assert.False(NativeMouseEventService.TryCreateScrollEvent(
            0x0200,
            mouseData: 0,
            flags: 0,
            x: 0,
            y: 0,
            pressedModifiers: default,
            out _));
    }

    [Fact]
    public void TryCreateScrollEventRejectsZeroDelta()
    {
        Assert.False(NativeMouseEventService.TryCreateScrollEvent(
            0x020A,
            mouseData: 0,
            flags: 0,
            x: 0,
            y: 0,
            pressedModifiers: default,
            out _));
    }

    [Fact]
    public void WatchScrollSharesSingleHookWithButtonHotkeys()
    {
        var platform = new FakeMouseHookPlatform();
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(NullRuntimeLogger.Instance, scheduler);
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null, dispatcher);
        var service = new NativeMouseEventService(hook);
        var seen = 0;

        using var scroll = service.WatchScroll(
            new MouseScrollWatchOptions(IncludeInjected: false, Blocking: true, Axes: MouseScrollAxis.Both),
            _ =>
            {
                seen++;
                return true;
            });
        using var button = hook.Register(
            Core.Hotkeys.HotkeyDefinition.CreateMouseButton(Core.Hotkeys.HotkeyModifiers.None, Core.Hotkeys.HotkeyMouseButton.XButton1),
            () => { });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, platform.SetHookExCount);

        Assert.Equal(new IntPtr(1), platform.InvokeWheel(0x020A, delta: 120));
        Assert.Equal(0, seen);
        var scheduled = Assert.Single(scheduler.Callbacks);
        scheduled();
        Assert.Equal(1, seen);
    }

    [Fact]
    public void PreventDefaultSwallowsWithoutInvokingCallbackOnHookThread()
    {
        var platform = new FakeMouseHookPlatform();
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(NullRuntimeLogger.Instance, scheduler);
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null, dispatcher);
        var service = new NativeMouseEventService(hook);
        var seen = 0;

        using var registration = service.WatchScroll(
            new MouseScrollWatchOptions(IncludeInjected: false, Blocking: true, Axes: MouseScrollAxis.Both),
            _ =>
            {
                seen++;
                return false;
            });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        var result = platform.InvokeWheel(0x020A, delta: 120);

        Assert.Equal(new IntPtr(1), result);
        Assert.Equal(0, seen);
        var scheduled = Assert.Single(scheduler.Callbacks);
        scheduled();
        Assert.Equal(1, seen);
    }

    [Fact]
    public void NonBlockingWatcherPassesWheelThroughAndSchedulesCallback()
    {
        var platform = new FakeMouseHookPlatform();
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(NullRuntimeLogger.Instance, scheduler);
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null, dispatcher);
        var service = new NativeMouseEventService(hook);
        var seen = 0;

        using var registration = service.WatchScroll(
            MouseScrollWatchOptions.Default,
            _ =>
            {
                seen++;
                return false;
            });

        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        var result = platform.InvokeWheel(0x020A, delta: -120);

        Assert.Equal(IntPtr.Zero, result);
        Assert.Equal(0, seen);
        var scheduled = Assert.Single(scheduler.Callbacks);
        scheduled();
        Assert.Equal(1, seen);
    }

    [Fact]
    public void LastWatchDisposeUnhooksWhenNoButtonHotkeysRemain()
    {
        var platform = new FakeMouseHookPlatform();
        var dispatcher = new MouseScrollWatchDispatcher(
            NullRuntimeLogger.Instance,
            new SynchronizationContextMouseScrollWatchCallbackScheduler(null));
        using var hook = new NativeMouseHotkeyHook(NullRuntimeLogger.Instance, platform, callbackContext: null, dispatcher);
        var service = new NativeMouseEventService(hook);

        var registration = service.WatchScroll(MouseScrollWatchOptions.Default, _ => false);
        Assert.True(platform.InstallCompleted.Wait(TimeSpan.FromSeconds(5)));
        registration.Dispose();

        Assert.Equal(1, platform.UnhookCount);
        Assert.Equal(1, platform.QuitPostedCount);
    }

    private sealed class CapturingScheduler : IMouseScrollWatchCallbackScheduler
    {
        public List<Action> Callbacks { get; } = [];

        public void Schedule(Action callback) => Callbacks.Add(callback);
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
            return new IntPtr(0x4321);
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

        public IntPtr InvokeWheel(int message, int delta)
        {
            if (_hookProcedure is null)
            {
                throw new InvalidOperationException("Hook procedure was not installed.");
            }

            var mouseData = unchecked((uint)((ushort)delta << 16));
            var hookData = new MouseHookData
            {
                X = 5,
                Y = 6,
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
}
