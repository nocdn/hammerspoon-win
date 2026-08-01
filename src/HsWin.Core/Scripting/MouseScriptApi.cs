using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.Core.Scripting;

public sealed class MouseScriptApi
{
    private readonly IMouseService _mouse;
    private readonly IMouseInputService _mouseInput;
    private readonly IRuntimeLogger _logger;
    private readonly Action<IDisposable> _trackResource;

    internal MouseScriptApi(
        IMouseService mouse,
        IMouseInputService mouseInput,
        IRuntimeLogger logger,
        Action<IDisposable> trackResource)
    {
        _mouse = mouse;
        _mouseInput = mouseInput;
        _logger = logger;
        _trackResource = trackResource;
    }

    public string GetCurrentScreenJson()
    {
        var screen = _mouse.GetCurrentScreen();
        if (screen is null)
        {
            _logger.Info("Script hs.mouse.getCurrentScreen() returned null.");
            return "null";
        }

        _logger.Info($"Script hs.mouse.getCurrentScreen() returned id='{screen.Id}' name='{screen.Name}'.");
        return ScriptJson.Serialize(screen);
    }

    public bool IsOnPrimaryScreen()
    {
        var result = _mouse.GetCurrentScreen()?.IsPrimary == true;
        _logger.Info($"Script hs.mouse.isOnPrimaryScreen() returned {result}.");
        return result;
    }

    public void Click(object? button)
    {
        var parsedButton = MouseButtonParser.Parse(button);
        _mouseInput.Click(parsedButton);
        _logger.Info($"Script hs.mouse.click('{MouseButtonParser.GetDisplayName(parsedButton)}') requested.");
    }

    public ScriptResourceHandle Repeat(object? button, object? options = null)
    {
        var parsedButton = MouseButtonParser.Parse(button);
        var parsedOptions = MouseScriptOptionsParser.ParseRepeatOptions(options);
        var handle = new ScriptResourceHandle(_mouseInput.Repeat(parsedButton, parsedOptions));
        _trackResource(handle);
        _logger.Info(
            $"Script hs.mouse.repeat('{MouseButtonParser.GetDisplayName(parsedButton)}') intervalMs={parsedOptions.IntervalMs}.");
        return handle;
    }
}
