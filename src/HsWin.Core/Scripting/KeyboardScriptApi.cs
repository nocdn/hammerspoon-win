using System.Globalization;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class KeyboardScriptApi
{
    private readonly IKeyboardEventService _keyboardEvents;
    private readonly IKeyboardInputService _keyboardInput;
    private readonly IRuntimeLogger _logger;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal KeyboardScriptApi(
        IKeyboardEventService keyboardEvents,
        IKeyboardInputService keyboardInput,
        IRuntimeLogger logger,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _keyboardEvents = keyboardEvents;
        _keyboardInput = keyboardInput;
        _logger = logger;
        _callbacks = callbacks;
        _trackResource = trackResource;
    }

    public ScriptResourceHandle Watch(object? callback, object? options = null)
    {
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Keyboard watch callback must be a JavaScript function.", nameof(callback));
        }

        var parsedOptions = KeyboardScriptOptionsParser.ParseWatchOptions(options);
        var registration = _keyboardEvents.Watch(
            parsedOptions,
            keyboardEvent =>
            {
                var eventJson = ScriptJson.Serialize(keyboardEvent);
                // Blocking watchers run on WH_KEYBOARD_LL — fail-open if the script gate is busy
                // so a wedged UI callback cannot freeze the physical keyboard.
                if (parsedOptions.Blocking)
                {
                    if (!_callbacks.TryInvokeScriptCallbackFailOpen(scriptFunction, out var blockingResult, eventJson))
                    {
                        return false; // pass key through
                    }

                    return Convert.ToBoolean(blockingResult, CultureInfo.InvariantCulture);
                }

                var result = _callbacks.InvokeScriptCallback(scriptFunction, eventJson);
                var requestedSwallow = Convert.ToBoolean(result, CultureInfo.InvariantCulture);

                if (requestedSwallow)
                {
                    _logger.Warning("Non-blocking hs.keyboard.watch() callback returned true; use { blocking: true } when a watcher must swallow keyboard input.");
                }

                return false;
            });

        var handle = new ScriptResourceHandle(registration);
        _trackResource(handle);
        _logger.Info(
            $"Script hs.keyboard.watch() registered includeInjected={parsedOptions.IncludeInjected} blocking={parsedOptions.Blocking} " +
            $"keys={FormatKeyFilter(parsedOptions.KeyFilter)}.");
        return handle;
    }

    public ScriptResourceHandle Remap(object? sourceKey, object? targetKey)
    {
        var sourceVirtualKey = HotkeyParser.ParseVirtualKey(sourceKey);
        var targetVirtualKey = HotkeyParser.ParseVirtualKey(targetKey);
        var sourceName = KeyboardKeyRules.GetDisplayName(sourceVirtualKey);
        var targetName = KeyboardKeyRules.GetDisplayName(targetVirtualKey);

        var registration = _keyboardEvents.Watch(
            new KeyboardEventWatchOptions(
                IncludeInjected: false,
                Blocking: true,
                KeyFilter: new HashSet<uint> { sourceVirtualKey },
                Prepend: true),
            keyboardEvent =>
            {
                if (keyboardEvent.KeyCode != sourceVirtualKey)
                {
                    return false;
                }

                _logger.Info(
                    $"Keyboard remap matched source='{sourceName}' target='{targetName}' type='{keyboardEvent.Type}' " +
                    $"sourceVk=0x{sourceVirtualKey:X2} targetVk=0x{targetVirtualKey:X2}.");
                if (keyboardEvent.IsKeyDown)
                {
                    _keyboardInput.Tap(targetVirtualKey, KeyboardTapOptions.Default);
                }

                return true;
            });

        var handle = new ScriptResourceHandle(registration);
        _trackResource(handle);
        _logger.Info(
            $"Script hs.keyboard.remap('{sourceName}', '{targetName}') registered sourceVk=0x{sourceVirtualKey:X2} targetVk=0x{targetVirtualKey:X2}.");
        return handle;
    }

    private static string FormatKeyFilter(IReadOnlySet<uint>? keyFilter)
    {
        return keyFilter is { Count: > 0 }
            ? string.Join(",", keyFilter.Select(key => $"0x{key:X2}"))
            : "<all>";
    }

    public void Tap(object? key, object? options = null)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        var parsedOptions = KeyboardScriptOptionsParser.ParseTapOptions(options);
        _keyboardInput.Tap(virtualKey, parsedOptions);
    }

    public KeyboardRepeatScriptHandle Repeat(object? key, object? options = null)
    {
        return StartRepeat(key, options, pulse: false);
    }

    public KeyboardRepeatScriptHandle RepeatPulse(object? key, object? options = null)
    {
        return StartRepeat(key, options, pulse: true);
    }

    private KeyboardRepeatScriptHandle StartRepeat(object? key, object? options, bool pulse)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        var parsedOptions = KeyboardScriptOptionsParser.ParseRepeatOptions(options);
        var displayName = KeyboardKeyRules.GetDisplayName(virtualKey);
        if (pulse)
        {
            if (parsedOptions.KeyDownMs <= 0)
            {
                throw new ArgumentException(
                    "hs.keyboard.repeatPulse() requires keyDownMs (or holdMs/pressDurationMs) greater than 0.",
                    nameof(options));
            }
        }
        else
        {
            if (parsedOptions.KeyDownMs > 0)
            {
                throw new ArgumentException(
                    "Use hs.keyboard.repeatPulse() for repeats with a held key-down phase.",
                    nameof(options));
            }

            if (KeyboardKeyRules.IsModifierVirtualKey(virtualKey))
            {
                _logger.Warning(
                    $"Rejected instantaneous modifier repeat key='{displayName}' vk=0x{virtualKey:X2}; " +
                    "use hs.keyboard.repeatPulse() with keyDownMs > 0.");
                throw new ArgumentException(
                    "Modifier repeats require hs.keyboard.repeatPulse() with keyDownMs greater than 0.",
                    nameof(key));
            }
        }

        var handle = new KeyboardRepeatScriptHandle(_keyboardInput.Repeat(virtualKey, parsedOptions));
        _trackResource(handle);
        var methodName = pulse ? "repeatPulse" : "repeat";
        _logger.Info(
            $"Script hs.keyboard.{methodName}('{displayName}') " +
            $"intervalMs={parsedOptions.IntervalMs} " +
            $"keyDownMs={parsedOptions.KeyDownMs} " +
            $"suppressModifiers=0x{(uint)parsedOptions.SuppressPhysicalModifiers:X} " +
            $"inputMethod={KeyboardInputMethodParser.GetDisplayName(parsedOptions.InputMethod)}.");
        return handle;
    }

    public void StopRepeat()
    {
        _keyboardInput.StopActiveRepeat();
        _logger.Info("Script hs.keyboard.stopRepeat() requested.");
    }

    public void KeyDown(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        _keyboardInput.KeyDown(virtualKey);
        _logger.Info($"Script hs.keyboard.keyDown('{KeyboardKeyRules.GetDisplayName(virtualKey)}') requested.");
    }

    public void KeyUp(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        _keyboardInput.KeyUp(virtualKey);
        _logger.Info($"Script hs.keyboard.keyUp('{KeyboardKeyRules.GetDisplayName(virtualKey)}') requested.");
    }

    public bool IsDown(object? key)
    {
        var virtualKey = HotkeyParser.ParseVirtualKey(key);
        return _keyboardEvents.IsKeyDown(virtualKey);
    }
}
