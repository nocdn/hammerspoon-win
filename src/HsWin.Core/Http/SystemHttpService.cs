using System.Net.Http.Headers;
using System.Text;
using HsWin.Core.Logging;

namespace HsWin.Core.Http;

public sealed class SystemHttpService : IHttpService
{
    private static readonly HttpClient HttpClient = new();

    private readonly IRuntimeLogger _logger;

    public SystemHttpService(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public IDisposable Send(HttpRequestOptions options, Action<HttpResponseSnapshot> callback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callback);

        var request = new HttpRequestTask(options, callback, _logger);
        request.Start();
        return request;
    }

    private sealed class HttpRequestTask : IDisposable
    {
        private readonly HttpRequestOptions _options;
        private readonly Action<HttpResponseSnapshot> _callback;
        private readonly IRuntimeLogger _logger;
        private readonly CancellationTokenSource _cancellation = new();
        private int _disposed;

        public HttpRequestTask(HttpRequestOptions options, Action<HttpResponseSnapshot> callback, IRuntimeLogger logger)
        {
            _options = options;
            _callback = callback;
            _logger = logger;
        }

        public void Start()
        {
            _ = Task.Run(SendAsync);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _cancellation.Cancel();
                _cancellation.Dispose();
            }
        }

        private async Task SendAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, timeout.Token);
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(_options.Method), BuildUri(_options));
                using var content = CreateContent(_options);
                if (content is not null)
                {
                    request.Content = content;
                }

                AddHeaders(request, _options.Headers);

                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
                var bodyBytes = await response.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
                var body = ReadBody(_options.ResponseType, bodyBytes, response.Content.Headers.ContentType?.CharSet);
                var headers = ReadHeaders(response);
                _logger.Info($"HTTP request completed method='{_options.Method}' url='{_options.Url}' statusCode={(int)response.StatusCode}.");
                InvokeCallback(new HttpResponseSnapshot(
                    response.IsSuccessStatusCode,
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? response.StatusCode.ToString(),
                    headers,
                    body,
                    TimedOut: false,
                    Error: null));
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && Volatile.Read(ref _disposed) == 0)
            {
                InvokeCallback(new HttpResponseSnapshot(false, null, "timeout", new Dictionary<string, string>(), string.Empty, TimedOut: true, "HTTP request timed out."));
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"HTTP request canceled method='{_options.Method}' url='{_options.Url}'.");
            }
            catch (Exception exception)
            {
                _logger.Error("HTTP request failed.", exception);
                InvokeCallback(new HttpResponseSnapshot(false, null, "error", new Dictionary<string, string>(), string.Empty, TimedOut: false, exception.Message));
            }
        }

        private void InvokeCallback(HttpResponseSnapshot response)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _callback(response);
            }
        }

        private static Uri BuildUri(HttpRequestOptions options)
        {
            var builder = new UriBuilder(options.Url);
            if (options.Query.Count == 0)
            {
                return builder.Uri;
            }

            var query = new StringBuilder(builder.Query.TrimStart('?'));
            foreach (var item in options.Query)
            {
                if (query.Length > 0)
                {
                    query.Append('&');
                }

                query
                    .Append(Uri.EscapeDataString(item.Key))
                    .Append('=')
                    .Append(Uri.EscapeDataString(item.Value));
            }

            builder.Query = query.ToString();
            return builder.Uri;
        }

        private static HttpContent? CreateContent(HttpRequestOptions options)
        {
            if (options.Multipart.Count > 0)
            {
                var multipart = new MultipartFormDataContent();
                foreach (var part in options.Multipart)
                {
                    if (!string.IsNullOrWhiteSpace(part.Path))
                    {
                        var filePath = Environment.ExpandEnvironmentVariables(part.Path);
                        var stream = File.OpenRead(filePath);
                        var fileContent = new StreamContent(stream);
                        if (!string.IsNullOrWhiteSpace(part.ContentType))
                        {
                            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(part.ContentType);
                        }

                        multipart.Add(fileContent, part.Name, part.FileName ?? Path.GetFileName(filePath));
                    }
                    else
                    {
                        multipart.Add(new StringContent(part.Value ?? string.Empty), part.Name);
                    }
                }

                return multipart;
            }

            if (options.Form.Count > 0)
            {
                return new FormUrlEncodedContent(options.Form);
            }

            if (options.Body is not null)
            {
                var content = new StringContent(options.Body, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(options.ContentType))
                {
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(options.ContentType);
                }

                return content;
            }

            return null;
        }

        private static void AddHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        private static string ReadBody(string responseType, byte[] bodyBytes, string? charset)
        {
            if (responseType.Equals("base64", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToBase64String(bodyBytes);
            }

            var encoding = !string.IsNullOrWhiteSpace(charset)
                ? Encoding.GetEncoding(charset)
                : Encoding.UTF8;
            return encoding.GetString(bodyBytes);
        }

        private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            return headers;
        }
    }
}
