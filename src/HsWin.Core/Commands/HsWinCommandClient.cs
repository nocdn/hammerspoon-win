using System.IO.Pipes;
using System.Text;

namespace HsWin.Core.Commands;

public sealed class HsWinCommandClient
{
    public const int DefaultTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _pipeName;

    public HsWinCommandClient()
        : this(HsWinCommandProtocol.PipeName)
    {
    }

    public HsWinCommandClient(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        _pipeName = pipeName;
    }

    public HsWinCommandResponse Send(HsWinCommandRequest request, int timeoutMs = DefaultTimeoutMs)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (timeoutMs < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be at least 1 millisecond.");
        }

        using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.None);
        pipe.Connect(timeoutMs);

        using var reader = new StreamReader(
            pipe,
            Utf8NoBom,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            Utf8NoBom,
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        writer.WriteLine(HsWinCommandProtocol.SerializeRequest(request));
        var responseJson = reader.ReadLine()
            ?? throw new IOException("The HsWin command server closed the connection before returning a response.");
        return HsWinCommandProtocol.DeserializeResponse(responseJson);
    }
}
