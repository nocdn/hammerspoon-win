using HsWin.Core.Clipboard;
using HsWin.Core.Logging;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfTextDataFormat = System.Windows.TextDataFormat;

namespace HsWin.App.Clipboard;

internal sealed class NativeClipboardService : IClipboardService
{
    private const int MaxAttempts = 5;

    private readonly Dispatcher _dispatcher;
    private readonly IRuntimeLogger _logger;

    public NativeClipboardService(Dispatcher dispatcher, IRuntimeLogger logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public string GetText()
    {
        return InvokeOnDispatcher(() =>
        {
            var text = WpfClipboard.ContainsText(WpfTextDataFormat.UnicodeText)
                ? WpfClipboard.GetText(WpfTextDataFormat.UnicodeText)
                : string.Empty;
            _logger.Info($"Clipboard text read length={text.Length}.");
            return text;
        });
    }

    public bool SetText(string text)
    {
        return InvokeOnDispatcher(() =>
        {
            if (text.Length == 0)
            {
                WpfClipboard.Clear();
            }
            else
            {
                WpfClipboard.SetText(text, WpfTextDataFormat.UnicodeText);
            }

            _logger.Info($"Clipboard text written length={text.Length}.");
            return true;
        });
    }

    private T InvokeOnDispatcher<T>(Func<T> action)
    {
        return _dispatcher.CheckAccess()
            ? RetryClipboardOperation(action)
            : _dispatcher.Invoke(() => RetryClipboardOperation(action));
    }

    private static T RetryClipboardOperation<T>(Func<T> action)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (ExternalException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(attempt * 20);
            }
            catch (COMException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(attempt * 20);
            }
        }

        return action();
    }
}
