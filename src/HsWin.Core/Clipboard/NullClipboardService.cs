namespace HsWin.Core.Clipboard;

public sealed class NullClipboardService : IClipboardService
{
    public static NullClipboardService Instance { get; } = new();

    private NullClipboardService()
    {
    }

    public string GetText()
    {
        throw new NotSupportedException("Clipboard access is not available in this runtime.");
    }

    public bool SetText(string text)
    {
        throw new NotSupportedException("Clipboard access is not available in this runtime.");
    }
}
