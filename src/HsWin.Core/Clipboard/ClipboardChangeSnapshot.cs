namespace HsWin.Core.Clipboard;

public sealed record ClipboardChangeSnapshot(
    long Sequence,
    string Contents,
    bool HasText);
