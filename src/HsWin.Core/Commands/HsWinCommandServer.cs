using System.IO.Pipes;
using System.Text;
using HsWin.Core.Logging;

namespace HsWin.Core.Commands;

public sealed class HsWinCommandServer : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _pipeName;
    private readonly Func<HsWinCommandRequest, HsWinCommandResponse> _handleRequest;
    private readonly IRuntimeLogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private Task? _runTask;
    private int _started;
    private bool _disposed;

    public HsWinCommandServer(
        Func<HsWinCommandRequest, HsWinCommandResponse> handleRequest,
        IRuntimeLogger logger)
        : this(HsWinCommandProtocol.PipeName, handleRequest, logger)
    {
    }

    public HsWinCommandServer(
        string pipeName,
        Func<HsWinCommandRequest, HsWinCommandResponse> handleRequest,
        IRuntimeLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(handleRequest);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeName = pipeName;
        _handleRequest = handleRequest;
        _logger = logger;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _runTask = Task.Run(() => RunAsync(_stop.Token));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _stop.Cancel();
        try
        {
            _runTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }

        _stop.Dispose();
        _disposed = true;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Error("HsWin command server connection failed.", exception);
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Utf8NoBom,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        using var writer = new StreamWriter(
            stream,
            Utf8NoBom,
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        var requestJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (requestJson is null)
        {
            return;
        }

        HsWinCommandResponse response;
        try
        {
            var request = HsWinCommandProtocol.DeserializeRequest(requestJson);
            response = _handleRequest(request);
        }
        catch (Exception exception)
        {
            _logger.Error("HsWin command request failed.", exception);
            response = HsWinCommandResponse.Error(exception.Message);
        }

        var responseJson = HsWinCommandProtocol.SerializeResponse(response);
        await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
