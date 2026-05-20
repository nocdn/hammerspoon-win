using HsWin.App.Hotkeys;
using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;

namespace HsWin.App.Tests;

public sealed class NativeHotkeyServiceTests
{
    [Fact]
    public void RegisterUsesThreadInvokerWhenCalledOffOwnerThread()
    {
        var invoker = new CapturingThreadInvoker { HasAccess = false };
        var platform = new CapturingHotkeyPlatform();
        using var service = new NativeHotkeyService(NullRuntimeLogger.Instance, invoker, platform);

        using var registration = service.Register(
            HotkeyDefinition.CreateKeyboard(HotkeyModifiers.None, 0xC0),
            () => { });

        Assert.Equal(1, invoker.FuncInvokeCount);
        var registered = Assert.Single(platform.RegisteredHotkeys);
        Assert.NotEqual(IntPtr.Zero, registered.WindowHandle);
        Assert.Equal(0x4000u, registered.Modifiers);
        Assert.Equal(0xC0u, registered.VirtualKey);
    }

    [Fact]
    public void RegistrationDisposeUsesThreadInvokerWhenCalledOffOwnerThread()
    {
        var invoker = new CapturingThreadInvoker { HasAccess = true };
        var platform = new CapturingHotkeyPlatform();
        using var service = new NativeHotkeyService(NullRuntimeLogger.Instance, invoker, platform);
        var registration = service.Register(
            HotkeyDefinition.CreateKeyboard(HotkeyModifiers.None, 0xC0),
            () => { });

        invoker.HasAccess = false;
        registration.Dispose();

        Assert.Equal(1, invoker.ActionInvokeCount);
        var unregistered = Assert.Single(platform.UnregisteredHotkeys);
        Assert.Equal(1, unregistered.Id);
    }

    [Fact]
    public void RegistrationFailureMessageOnlyReportsAlreadyInUseForHotkeyCollision()
    {
        var hotkey = HotkeyDefinition.CreateKeyboard(HotkeyModifiers.None, 0xC0);

        Assert.Equal(
            "Hotkey already in use: None+0xC0.",
            NativeHotkeyService.CreateRegistrationFailureMessage(1409, hotkey));
        Assert.Equal(
            "Could not register hotkey None+0xC0.",
            NativeHotkeyService.CreateRegistrationFailureMessage(1408, hotkey));
    }

    private sealed class CapturingThreadInvoker : NativeHotkeyService.IHotkeyThreadInvoker
    {
        public bool HasAccess { get; set; }

        public int FuncInvokeCount { get; private set; }

        public int ActionInvokeCount { get; private set; }

        public bool CheckAccess()
        {
            return HasAccess;
        }

        public T Invoke<T>(Func<T> callback)
        {
            FuncInvokeCount++;
            return callback();
        }

        public void Invoke(Action callback)
        {
            ActionInvokeCount++;
            callback();
        }
    }

    private sealed class CapturingHotkeyPlatform : NativeHotkeyService.IHotkeyPlatform
    {
        public List<RegisteredHotkey> RegisteredHotkeys { get; } = [];

        public List<UnregisteredHotkey> UnregisteredHotkeys { get; } = [];

        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey, out int errorCode)
        {
            RegisteredHotkeys.Add(new RegisteredHotkey(windowHandle, id, modifiers, virtualKey));
            errorCode = 0;
            return true;
        }

        public bool UnregisterHotKey(IntPtr windowHandle, int id, out int errorCode)
        {
            UnregisteredHotkeys.Add(new UnregisteredHotkey(windowHandle, id));
            errorCode = 0;
            return true;
        }
    }

    private sealed record RegisteredHotkey(IntPtr WindowHandle, int Id, uint Modifiers, uint VirtualKey);

    private sealed record UnregisteredHotkey(IntPtr WindowHandle, int Id);
}
