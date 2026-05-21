namespace HsWin.Core.Http;

public sealed record HttpResponseSnapshot(
    bool Success,
    int? StatusCode,
    string Status,
    IReadOnlyDictionary<string, string> Headers,
    string Body,
    bool TimedOut,
    string? Error);
