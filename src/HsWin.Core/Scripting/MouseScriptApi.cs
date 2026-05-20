using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.Core.Scripting;

public sealed class MouseScriptApi
{
    private readonly IMouseService _mouse;
    private readonly IRuntimeLogger _logger;

    public MouseScriptApi(IMouseService mouse, IRuntimeLogger logger)
    {
        _mouse = mouse;
        _logger = logger;
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
}
