namespace HsWin.Core.Http;

public interface IHttpService
{
    IDisposable Send(HttpRequestOptions options, Action<HttpResponseSnapshot> callback);
}
