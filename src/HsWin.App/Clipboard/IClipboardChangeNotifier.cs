namespace HsWin.App.Clipboard;

internal interface IClipboardChangeNotifier : IDisposable
{
    IDisposable Watch(Action changed);
}
