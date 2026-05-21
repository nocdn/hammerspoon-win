namespace HsWin.Core.Http;

public sealed record HttpRequestOptions(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    string? Body,
    string? ContentType,
    IReadOnlyDictionary<string, string> Form,
    IReadOnlyList<HttpMultipartPart> Multipart,
    int TimeoutMs,
    string ResponseType)
{
    public const int DefaultTimeoutMs = 30000;
}
