namespace HsWin.Core.Http;

public sealed class NullHttpService : IHttpService
{
    public static NullHttpService Instance { get; } = new();

    private NullHttpService()
    {
    }

    public IDisposable Send(HttpRequestOptions options, Action<HttpResponseSnapshot> callback)
    {
        throw new NotSupportedException("HTTP requests are not available in this runtime.");
    }
}
