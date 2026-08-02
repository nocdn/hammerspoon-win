namespace HsWin.Core.Mouse;

public interface IMouseEventService
{
    IDisposable WatchScroll(MouseScrollWatchOptions options, Func<MouseScrollEventSnapshot, bool> callback);
}
