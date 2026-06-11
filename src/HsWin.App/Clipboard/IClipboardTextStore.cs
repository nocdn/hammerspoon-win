namespace HsWin.App.Clipboard;

internal interface IClipboardTextStore
{
    ClipboardTextContents Read();

    bool Write(string text);
}
