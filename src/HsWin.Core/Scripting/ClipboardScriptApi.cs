using HsWin.Core.Clipboard;
using HsWin.Core.Logging;

namespace HsWin.Core.Scripting;

public sealed class ClipboardScriptApi
{
    private readonly IClipboardService _clipboard;
    private readonly IRuntimeLogger _logger;

    public ClipboardScriptApi(IClipboardService clipboard, IRuntimeLogger logger)
    {
        _clipboard = clipboard;
        _logger = logger;
    }

    public string GetText()
    {
        var text = _clipboard.GetText();
        _logger.Info($"Script hs.pasteboard.getContents() returned {text.Length} characters.");
        return text;
    }

    public bool SetText(object? text)
    {
        var clipboardText = ScriptArgumentReader.RequireText(text, "text");
        var result = _clipboard.SetText(clipboardText);
        _logger.Info($"Script hs.pasteboard.setContents() wrote {clipboardText.Length} characters result={result}.");
        return result;
    }
}
