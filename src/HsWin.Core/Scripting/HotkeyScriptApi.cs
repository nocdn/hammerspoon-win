using HsWin.Core.Hotkeys;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class HotkeyScriptApi
{
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal HotkeyScriptApi(
        IHotkeyRegistrar hotkeys,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _hotkeys = hotkeys;
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
}
