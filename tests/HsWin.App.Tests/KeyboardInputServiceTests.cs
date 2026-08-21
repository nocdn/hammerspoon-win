using HsWin.App.Input;
using HsWin.App.Keyboard;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Tests;

public sealed class KeyboardInputServiceTests
{
    [Fact]
    public void TapOutsideKeyboardHookDispatchSendsImmediately()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);

        service.Tap(0x23, KeyboardTapOptions.Default);

        var action = Assert.Single(sender.Actions);
        Assert.Equal("tap:0x23:sendInput", action);
        Assert.DoesNotContain(logger.Infos, info => info.Contains("deferred", StringComparison.Ordinal));
    }

    [Fact]
    public void TapInsideKeyboardHookDispatchDefersUntilHookReturns()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);

        using (var scope = KeyboardHookDispatchScope.Enter(
                   logger,
                   new KeyboardEventSnapshot("keydown", 0x21, "pageup", [], 0, true, false, false, false, false),
                   scanCode: 0x49,
                   flags: 0x10,
                   message: 0x0100))
        {
            service.Tap(0x23, KeyboardTapOptions.Default);

            Assert.Empty(sender.Actions);
            Assert.Contains(
                logger.Infos,
                info => info.Contains("Keyboard remap input deferred", StringComparison.Ordinal)
                    && info.Contains("tap key='end' vk=0x23", StringComparison.Ordinal));
        }

        Assert.True(sender.WaitForAction(TimeSpan.FromSeconds(2)));
        var action = Assert.Single(sender.Actions);
        Assert.Equal("tap:0x23:sendInput", action);
        Assert.Contains(logger.Infos, info => info.Contains("deferred input executing", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, info => info.Contains("deferred input completed", StringComparison.Ordinal));
    }

    [Fact]
    public void TapAfterKeyboardHookDispatchDisposedSendsImmediately()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);

        using (KeyboardHookDispatchScope.Enter(
                   logger,
                   new KeyboardEventSnapshot("keydown", 0x08, "backspace", [], 0, true, false, false, false, false),
                   scanCode: 0x0E,
                   flags: 0,
                   message: 0x0100))
        {
        }

        service.Tap(0x24, KeyboardTapOptions.Default);

        var action = Assert.Single(sender.Actions);
        Assert.Equal("tap:0x24:sendInput", action);
        Assert.DoesNotContain(
            logger.Infos,
            info => info.Contains("Keyboard remap input deferred", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TapFromAnotherThreadDuringKeyboardHookDispatchSendsImmediately()
    {
        // The dispatch scope is local to the hook thread by design: only input sent
        // synchronously by the hook dispatch itself (remaps) is deferred. Script callbacks
        // scheduled onto other threads send inline.
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);

        using (KeyboardHookDispatchScope.Enter(
                   logger,
                   new KeyboardEventSnapshot("keydown", 0x42, "b", [], 0, true, false, false, false, false),
                   scanCode: 0x30,
                   flags: 0,
                   message: 0x0100))
        {
            await Task.Run(() => service.Tap(0x24, KeyboardTapOptions.Default));

            var action = Assert.Single(sender.Actions);
            Assert.Equal("tap:0x24:sendInput", action);
            Assert.DoesNotContain(
                logger.Infos,
                info => info.Contains("Keyboard remap input deferred", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RepeatPassesTheRequestedInputMethodToTheSender()
    {
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(NullRuntimeLogger.Instance, sender);

        using var repeat = service.Repeat(
            0xA0,
            new KeyboardRepeatOptions(10, HotkeyModifiers.None, KeyboardInputMethod.WindowMessage));

        Assert.True(sender.WaitForActionCount(2, TimeSpan.FromSeconds(2)));
        repeat.Dispose();

        Assert.All(
            sender.Actions.Where(action => action.StartsWith("tap:", StringComparison.Ordinal)),
            action => Assert.EndsWith(":windowMessage", action, StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatWithKeyDownDurationSendsObservableDownAndUpPhases()
    {
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(NullRuntimeLogger.Instance, sender);

        using var repeat = service.Repeat(
            KeyboardKeyRules.VkShift,
            new KeyboardRepeatOptions(
                IntervalMs: 80,
                SuppressPhysicalModifiers: HotkeyModifiers.None,
                InputMethod: KeyboardInputMethod.WindowMessage,
                KeyDownMs: 30));

        Assert.True(sender.WaitForActionCount(3, TimeSpan.FromSeconds(2)));

        Assert.Equal(
            [
                "down:0x10:windowMessage",
                "up:0x10:windowMessage",
                "down:0x10:windowMessage"
            ],
            sender.Actions.Take(3));
    }

    [Fact]
    public async Task RepeatPulseIsObservableToFiftyMillisecondStateSampler()
    {
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(NullRuntimeLogger.Instance, sender);
        var sampledStates = new List<bool>();

        using var repeat = service.Repeat(
            KeyboardKeyRules.VkShift,
            new KeyboardRepeatOptions(
                IntervalMs: 120,
                SuppressPhysicalModifiers: HotkeyModifiers.None,
                InputMethod: KeyboardInputMethod.SendInput,
                KeyDownMs: 60));

        for (var sample = 0; sample < 8; sample++)
        {
            await Task.Delay(50);
            sampledStates.Add(sender.IsDown(KeyboardKeyRules.VkShift));
        }

        Assert.Contains(true, sampledStates);
        Assert.Contains(false, sampledStates);
    }

    [Fact]
    public void RepeatRejectsIntervalNotLongerThanKeyDownDuration()
    {
        var service = new KeyboardInputService(
            NullRuntimeLogger.Instance,
            new CapturingKeyboardInputSender());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Repeat(
                KeyboardKeyRules.VkShift,
                new KeyboardRepeatOptions(
                    IntervalMs: 60,
                    SuppressPhysicalModifiers: HotkeyModifiers.None,
                    KeyDownMs: 60)));
    }

    [Fact]
    public void RepeatRegistersNativeFallbackForSuppressedPhysicalModifiers()
    {
        var sender = new CapturingKeyboardInputSender();
        var keyboardEvents = new CapturingKeyboardEventService();
        var service = new KeyboardInputService(NullRuntimeLogger.Instance, sender, keyboardEvents);

        using var repeat = service.Repeat(
            KeyboardKeyRules.VkShift,
            new KeyboardRepeatOptions(
                IntervalMs: 100,
                SuppressPhysicalModifiers: HotkeyModifiers.Control | HotkeyModifiers.Shift));

        Assert.NotNull(keyboardEvents.Options);
        Assert.True(keyboardEvents.Options.Blocking);
        Assert.Contains(KeyboardKeyRules.VkLeftControl, keyboardEvents.Options.KeyFilter!);
        Assert.Contains(KeyboardKeyRules.VkRightShift, keyboardEvents.Options.KeyFilter!);
        Assert.True(keyboardEvents.Invoke(CreateKeyDownSnapshot(KeyboardKeyRules.VkLeftControl)));

        repeat.Dispose();

        Assert.True(keyboardEvents.RegistrationDisposed);
    }

    [Theory]
    [InlineData(KeyboardKeyRules.VkLeftShift, KeyboardKeyRules.VkShift)]
    [InlineData(KeyboardKeyRules.VkRightShift, KeyboardKeyRules.VkShift)]
    [InlineData(KeyboardKeyRules.VkLeftControl, KeyboardKeyRules.VkControl)]
    [InlineData(KeyboardKeyRules.VkRightMenu, KeyboardKeyRules.VkMenu)]
    [InlineData(0x41u, 0x41u)]
    public void WindowMessagesUseGenericModifierVirtualKeys(uint input, uint expected)
    {
        Assert.Equal(expected, WindowMessageKeyboardInputSender.ResolveMessageVirtualKey(input));
    }

    private static KeyboardEventSnapshot CreateKeyDownSnapshot(uint virtualKey)
    {
        return new KeyboardEventSnapshot(
            Type: "keydown",
            KeyCode: virtualKey,
            Key: KeyboardKeyRules.GetDisplayName(virtualKey),
            Modifiers: [],
            ModifierFlags: 0,
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: KeyboardKeyRules.IsModifierVirtualKey(virtualKey),
            IsInjected: false,
            IsExtended: KeyboardKeyRules.IsExtendedVirtualKey(virtualKey));
    }

    private sealed class CapturingKeyboardInputSender : IKeyboardInputSender
    {
        private readonly ManualResetEventSlim _actionSent = new();
        private readonly object _gate = new();
        private readonly List<string> _actions = [];
        private readonly HashSet<uint> _downKeys = [];

        public IReadOnlyList<string> Actions
        {
            get
            {
                lock (_gate)
                {
                    return [.. _actions];
                }
            }
        }

        public void SendKeyDown(
            uint virtualKey,
            KeyboardInputMethod inputMethod = KeyboardInputMethod.SendInput,
            IRuntimeLogger? logger = null)
        {
            Add(
                $"down:0x{virtualKey:X2}:{KeyboardInputMethodParser.GetDisplayName(inputMethod)}",
                virtualKey,
                isDown: true);
        }

        public void SendKeyUp(
            uint virtualKey,
            KeyboardInputMethod inputMethod = KeyboardInputMethod.SendInput,
            IRuntimeLogger? logger = null)
        {
            Add(
                $"up:0x{virtualKey:X2}:{KeyboardInputMethodParser.GetDisplayName(inputMethod)}",
                virtualKey,
                isDown: false);
        }

        public void SendTap(
            uint virtualKey,
            IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
            IReadOnlyList<uint>? modifierVirtualKeys = null,
            KeyboardInputMethod inputMethod = KeyboardInputMethod.SendInput,
            IRuntimeLogger? logger = null)
        {
            Add($"tap:0x{virtualKey:X2}:{KeyboardInputMethodParser.GetDisplayName(inputMethod)}");
        }

        public bool WaitForAction(TimeSpan timeout)
        {
            return _actionSent.Wait(timeout);
        }

        public bool WaitForActionCount(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (_gate)
                {
                    if (_actions.Count >= count)
                    {
                        return true;
                    }
                }

                _actionSent.Wait(TimeSpan.FromMilliseconds(25));
                _actionSent.Reset();
            }

            lock (_gate)
            {
                return _actions.Count >= count;
            }
        }

        public bool IsDown(uint virtualKey)
        {
            lock (_gate)
            {
                return _downKeys.Contains(virtualKey);
            }
        }

        private void Add(string action, uint? virtualKey = null, bool? isDown = null)
        {
            lock (_gate)
            {
                _actions.Add(action);
                if (virtualKey is not null && isDown is not null)
                {
                    if (isDown.Value)
                    {
                        _downKeys.Add(virtualKey.Value);
                    }
                    else
                    {
                        _downKeys.Remove(virtualKey.Value);
                    }
                }
            }

            _actionSent.Set();
        }
    }

    private sealed class CapturingKeyboardEventService : IKeyboardEventService
    {
        private Func<KeyboardEventSnapshot, bool>? _callback;

        public KeyboardEventWatchOptions? Options { get; private set; }

        public bool RegistrationDisposed { get; private set; }

        public IDisposable Watch(
            KeyboardEventWatchOptions options,
            Func<KeyboardEventSnapshot, bool> callback)
        {
            Options = options;
            _callback = callback;
            return new CallbackDisposable(() => RegistrationDisposed = true);
        }

        public bool IsKeyDown(uint virtualKey) => false;

        public bool Invoke(KeyboardEventSnapshot snapshot)
        {
            return Assert.IsType<Func<KeyboardEventSnapshot, bool>>(_callback)(snapshot);
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    private sealed class CapturingRuntimeLogger : IRuntimeLogger
    {
        public List<string> Infos { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Errors { get; } = [];

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add($"{message} {exception.Message}");
        }
    }

    [Fact]
    public void NestedKeyboardHookDispatchScopesRestoreCorrectly()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);
        var outerSnapshot = new KeyboardEventSnapshot("keydown", 0x42, "b", [], 0, true, false, false, false, false);
        var innerSnapshot = new KeyboardEventSnapshot("keydown", 0x43, "c", [], 0, true, false, false, false, false);

        using (KeyboardHookDispatchScope.Enter(logger, outerSnapshot, 0x30, 0, 0x0100))
        {
            using (KeyboardHookDispatchScope.Enter(logger, innerSnapshot, 0x2E, 0, 0x0100))
            {
                service.Tap(0x23, KeyboardTapOptions.Default);
                Assert.Empty(sender.Actions);
            }

            // Inner scope disposed: the outer scope must still capture deferrals.
            service.Tap(0x24, KeyboardTapOptions.Default);
            Assert.Empty(sender.Actions);
        }

        Assert.True(sender.WaitForActionCount(2, TimeSpan.FromSeconds(2)));
    }
}
