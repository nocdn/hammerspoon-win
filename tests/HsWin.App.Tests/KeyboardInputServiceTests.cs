using HsWin.App.Input;
using HsWin.App.Keyboard;
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
        Assert.Equal("tap:0x23", action);
        Assert.DoesNotContain(logger.Infos, info => info.Contains("deferred", StringComparison.Ordinal));
    }

    [Fact]
    public void TapInsideKeyboardHookDispatchDefersUntilHookReturns()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);

        using (var scope = KeyboardHookDispatchScope.Enter(logger, "key='pageup' type='keydown' vk=0x21"))
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
        Assert.Equal("tap:0x23", action);
        Assert.Contains(logger.Infos, info => info.Contains("deferred input executing", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, info => info.Contains("deferred input completed", StringComparison.Ordinal));
    }

    [Fact]
    public void TapWithCapturedDisposedKeyboardHookDispatchSendsImmediately()
    {
        var logger = new CapturingRuntimeLogger();
        var sender = new CapturingKeyboardInputSender();
        var service = new KeyboardInputService(logger, sender);
        ExecutionContext? capturedContext;

        using (KeyboardHookDispatchScope.Enter(logger, "key='backspace' type='keydown' vk=0x08"))
        {
            capturedContext = ExecutionContext.Capture();
        }

        Assert.NotNull(capturedContext);
        ExecutionContext.Run(capturedContext, _ => service.Tap(0x24, KeyboardTapOptions.Default), null);

        var action = Assert.Single(sender.Actions);
        Assert.Equal("tap:0x24", action);
        Assert.DoesNotContain(
            logger.Infos,
            info => info.Contains("Keyboard remap input deferred", StringComparison.Ordinal));
    }

    private sealed class CapturingKeyboardInputSender : IKeyboardInputSender
    {
        private readonly ManualResetEventSlim _actionSent = new();
        private readonly object _gate = new();
        private readonly List<string> _actions = [];

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

        public void SendKeyDown(uint virtualKey, IRuntimeLogger? logger = null)
        {
            Add($"down:0x{virtualKey:X2}");
        }

        public void SendKeyUp(uint virtualKey, IRuntimeLogger? logger = null)
        {
            Add($"up:0x{virtualKey:X2}");
        }

        public void SendTap(
            uint virtualKey,
            IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
            IReadOnlyList<uint>? modifierVirtualKeys = null,
            IRuntimeLogger? logger = null)
        {
            Add($"tap:0x{virtualKey:X2}");
        }

        public bool WaitForAction(TimeSpan timeout)
        {
            return _actionSent.Wait(timeout);
        }

        private void Add(string action)
        {
            lock (_gate)
            {
                _actions.Add(action);
            }

            _actionSent.Set();
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
}
