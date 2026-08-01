using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using Microsoft.ClearScript;
using System.Globalization;

namespace HsWin.Core.Scripting;

public sealed class HotkeyScriptApi
{
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IKeyboardEventService _keyboardEvents;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal HotkeyScriptApi(
        IHotkeyRegistrar hotkeys,
        IKeyboardEventService keyboardEvents,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _hotkeys = hotkeys;
        _keyboardEvents = keyboardEvents;
        _callbacks = callbacks;
        _trackResource = trackResource;
    }

    public IDisposable Bind(object? modifiers, object? key, object? pressedFn)
    {
        if (pressedFn is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Hotkey callback must be a JavaScript function.", nameof(pressedFn));
        }

        var definition = HotkeyParser.Parse(modifiers, key);
        var registration = _hotkeys.Register(definition, () => _callbacks.InvokeHotkeyCallback(scriptFunction));
        _trackResource(registration);
        return registration;
    }

    public ScriptResourceHandle BindHeld(object? modifiers, object? key, object? pressedFn, object? releasedFn, object? options = null)
    {
        if (pressedFn is not ScriptObject pressedFunction)
        {
            throw new ArgumentException("Held hotkey pressed callback must be a JavaScript function.", nameof(pressedFn));
        }

        if (releasedFn is not ScriptObject releasedFunction)
        {
            throw new ArgumentException("Held hotkey released callback must be a JavaScript function.", nameof(releasedFn));
        }

        var parsedOptions = ParseHeldOptions(options);
        var definition = HotkeyParser.Parse(modifiers, key);
        if (definition.InputKind == HotkeyInputKind.MouseButton)
        {
            if (parsedOptions.Repeat)
            {
                throw new ArgumentException("Mouse held hotkeys do not support repeat/retrigger.", nameof(options));
            }

            var mouseRegistration = _hotkeys.RegisterHeld(
                definition,
                () => _callbacks.InvokeScriptCallback(
                    pressedFunction,
                    SerializeMouseEvent(definition, "mousedown", isDown: true)),
                () => _callbacks.InvokeScriptCallback(
                    releasedFunction,
                    SerializeMouseEvent(definition, "mouseup", isDown: false)),
                parsedOptions.Blocking);
            var mouseHandle = new ScriptResourceHandle(mouseRegistration);
            _trackResource(mouseHandle);
            return mouseHandle;
        }

        if (KeyboardKeyRules.IsModifierVirtualKey(definition.VirtualKey))
        {
            throw new ArgumentException("Held hotkey key must be a non-modifier key.", nameof(key));
        }

        var handler = new HeldHotkeyHandler(definition, parsedOptions, pressedFunction, releasedFunction, _callbacks);
        var watch = _keyboardEvents.Watch(
            new KeyboardEventWatchOptions(parsedOptions.IncludeInjected, parsedOptions.Blocking),
            handler.Handle);
        var handle = new ScriptResourceHandle(watch);
        _trackResource(handle);
        return handle;
    }

    private static string SerializeMouseEvent(HotkeyDefinition definition, string type, bool isDown)
    {
        var button = definition.MouseButton switch
        {
            HotkeyMouseButton.Middle => "middle",
            HotkeyMouseButton.XButton1 => "back",
            HotkeyMouseButton.XButton2 => "forward",
            _ => throw new ArgumentException("Mouse held hotkey requires a supported mouse button.", nameof(definition))
        };

        return ScriptJson.Serialize(new
        {
            type,
            button,
            isDown,
            isUp = !isDown
        });
    }

    private static HotkeyHeldOptions ParseHeldOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return HotkeyHeldOptions.Default;
        }

        return new HotkeyHeldOptions(
            ConvertOptionalBoolean(ScriptArgumentReader.GetPropertyValue(value, "includeInjected"), HotkeyHeldOptions.Default.IncludeInjected),
            ConvertOptionalBoolean(ScriptArgumentReader.GetPropertyValue(value, "blocking", "swallow"), HotkeyHeldOptions.Default.Blocking),
            ConvertOptionalBoolean(ScriptArgumentReader.GetPropertyValue(value, "allowExtraModifiers", "allowExtra"), HotkeyHeldOptions.Default.AllowExtraModifiers),
            ConvertOptionalBoolean(ScriptArgumentReader.GetPropertyValue(value, "repeat", "retrigger"), HotkeyHeldOptions.Default.Repeat));
    }

    private static bool ConvertOptionalBoolean(object? value, bool defaultValue)
    {
        return ScriptArgumentReader.IsMissing(value)
            ? defaultValue
            : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private sealed class HeldHotkeyHandler
    {
        private readonly HotkeyDefinition _definition;
        private readonly HotkeyHeldOptions _options;
        private readonly ScriptObject _pressedFunction;
        private readonly ScriptObject _releasedFunction;
        private readonly ScriptCallbackInvoker _callbacks;
        private bool _active;

        public HeldHotkeyHandler(
            HotkeyDefinition definition,
            HotkeyHeldOptions options,
            ScriptObject pressedFunction,
            ScriptObject releasedFunction,
            ScriptCallbackInvoker callbacks)
        {
            _definition = definition;
            _options = options;
            _pressedFunction = pressedFunction;
            _releasedFunction = releasedFunction;
            _callbacks = callbacks;
        }

        public bool Handle(KeyboardEventSnapshot snapshot)
        {
            if (_active && ShouldRelease(snapshot))
            {
                _active = false;
                _callbacks.InvokeScriptCallback(_releasedFunction, ScriptJson.Serialize(snapshot));
                return _options.Blocking;
            }

            if (snapshot.IsKeyDown && snapshot.KeyCode == _definition.VirtualKey && ModifiersMatch(snapshot.ModifierFlags))
            {
                if (!_active || _options.Repeat)
                {
                    _active = true;
                    _callbacks.InvokeScriptCallback(_pressedFunction, ScriptJson.Serialize(snapshot));
                }

                return _options.Blocking;
            }

            return false;
        }

        private bool ShouldRelease(KeyboardEventSnapshot snapshot)
        {
            return snapshot.IsKeyUp && snapshot.KeyCode == _definition.VirtualKey
                || snapshot.IsModifier && !ModifiersMatch(snapshot.ModifierFlags);
        }

        private bool ModifiersMatch(uint modifierFlags)
        {
            var modifiers = (HotkeyModifiers)modifierFlags;
            return _options.AllowExtraModifiers
                ? (modifiers & _definition.Modifiers) == _definition.Modifiers
                : modifiers == _definition.Modifiers;
        }
    }
}
