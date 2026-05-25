namespace HsWin.Core.Windows;

public interface IWindowService
{
    WindowSnapshot? GetFocusedWindow();

    WindowSnapshot? GetWindow(string id);

    WindowMoveResult MoveToScreen(string id, WindowTargetScreen targetScreen, WindowMoveOptions options);

    IDisposable WatchFocused(Action<WindowSnapshot> callback);
}
