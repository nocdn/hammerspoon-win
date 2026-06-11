using HsWin.Core.Clipboard;
using HsWin.Core.Logging;
using System.Windows.Threading;

namespace HsWin.App.Clipboard;

internal sealed class NativeClipboardService : IClipboardService, IDisposable
{
    private readonly IClipboardTextStore _textStore;
    private readonly IClipboardChangeNotifier _changeNotifier;
    private readonly IRuntimeLogger _logger;
    private long _sequence;
    private bool _disposed;

    public NativeClipboardService(Dispatcher dispatcher, IRuntimeLogger logger)
        : this(
            new WpfClipboardTextStore(dispatcher, logger),
            new NativeClipboardChangeNotifier(dispatcher, logger),
            logger)
    {
    }

    internal NativeClipboardService(
        IClipboardTextStore textStore,
        IClipboardChangeNotifier changeNotifier,
        IRuntimeLogger logger)
    {
        _textStore = textStore;
        _changeNotifier = changeNotifier;
        _logger = logger;
    }

    public string GetText()
    {
        return _textStore.Read().Text;
    }

    public bool SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _textStore.Write(text);
    }

    public IDisposable Watch(Action<ClipboardChangeSnapshot> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);

        return _changeNotifier.Watch(() =>
        {
            var contents = _textStore.Read();
            var snapshot = new ClipboardChangeSnapshot(
                Interlocked.Increment(ref _sequence),
                contents.Text,
                contents.HasText);
            _logger.Info(
                $"Clipboard change snapshot sequence={snapshot.Sequence} length={snapshot.Contents.Length} hasText={snapshot.HasText}.");
            callback(snapshot);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _changeNotifier.Dispose();
        _disposed = true;
    }
}
