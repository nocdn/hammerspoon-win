using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Input;

internal interface IMouseInputSender
{
    void SendClick(MouseButton button, MouseInputMethod inputMethod, IRuntimeLogger? logger = null);
}
