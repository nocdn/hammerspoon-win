namespace HsWin.Core.Http;

public sealed record HttpMultipartPart(
    string Name,
    string? Value,
    string? Path,
    string? FileName,
    string? ContentType);
