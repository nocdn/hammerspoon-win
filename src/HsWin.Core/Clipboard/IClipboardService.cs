namespace HsWin.Core.Clipboard;

public interface IClipboardService
{
    string GetText();

    bool SetText(string text);

    IDisposable Watch(Action<ClipboardChangeSnapshot> callback);
}
