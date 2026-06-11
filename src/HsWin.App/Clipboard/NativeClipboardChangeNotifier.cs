using HsWin.Core.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace HsWin.App.Clipboard;

internal sealed class NativeClipboardChangeNotifier : IClipboardChangeNotifier
{
    private const int WmClipboardUpdate = 0x031D;

    private readonly Dispatcher _dispatcher;
    private readonly IRuntimeLogger _logger;
    private readonly List<ClipboardChangeSubscription> _subscriptions = [];

    private ClipboardMessageWindow? _window;
    private long _nextSubscriptionId;
    private bool _disposed;

    public NativeClipboardChangeNotifier(Dispatcher dispatcher, IRuntimeLogger logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public IDisposable Watch(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        return InvokeOnDispatcher(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureWindow();

            var subscription = new ClipboardChangeSubscription(
                Interlocked.Increment(ref _nextSubscriptionId),
                changed,
                RemoveSubscription);
            _subscriptions.Add(subscription);
            _logger.Info($"Clipboard watcher registered id={subscription.Id} count={_subscriptions.Count}.");
            return subscription;
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            _disposed = true;
            return;
        }

        InvokeOnDispatcher(DisposeOnDispatcher);
    }

    private void EnsureWindow()
    {
        _window ??= new ClipboardMessageWindow(DispatchChanged, _logger);
    }

    private void RemoveSubscription(ClipboardChangeSubscription subscription)
    {
        if (_disposed || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            subscription.MarkDisposed();
            return;
        }

        InvokeOnDispatcher(() =>
        {
            if (_subscriptions.Remove(subscription))
            {
                _logger.Info($"Clipboard watcher unregistered id={subscription.Id} count={_subscriptions.Count}.");
            }

            subscription.MarkDisposed();
            if (_subscriptions.Count == 0)
            {
                DestroyWindow();
            }
        });
    }

    private void DispatchChanged()
    {
        var subscriptions = _subscriptions.ToArray();
        _logger.Info($"Clipboard change received watcherCount={subscriptions.Length}.");
        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Notify();
            }
            catch (Exception exception)
            {
                _logger.Warning($"Clipboard watcher callback failed id={subscription.Id}. {exception.Message}");
            }
        }
    }

    private void DisposeOnDispatcher()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.MarkDisposed();
        }

        _subscriptions.Clear();
        DestroyWindow();
        _disposed = true;
    }

    private void DestroyWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Dispose();
        _window = null;
        _logger.Info("Clipboard listener window destroyed.");
    }

    private T InvokeOnDispatcher<T>(Func<T> action)
    {
        return _dispatcher.CheckAccess()
            ? action()
            : _dispatcher.Invoke(action);
    }

    private void InvokeOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }

    private sealed class ClipboardMessageWindow : NativeWindow, IDisposable
    {
        private static readonly IntPtr MessageOnlyWindow = new(-3);

        private readonly Action _changed;
        private readonly IRuntimeLogger _logger;
        private bool _disposed;

        public ClipboardMessageWindow(Action changed, IRuntimeLogger logger)
        {
            _changed = changed;
            _logger = logger;
            CreateHandle(new CreateParams
            {
                Caption = $"{AppBranding.DisplayName} Clipboard Message Window",
                Parent = MessageOnlyWindow
            });

            if (!AddClipboardFormatListener(Handle))
            {
                var exception = new Win32Exception(Marshal.GetLastWin32Error(), "Could not register for clipboard updates.");
                _logger.Error("Clipboard listener registration failed.", exception);
                DestroyHandle();
                throw exception;
            }

            _logger.Info($"Clipboard listener window created HWND=0x{Handle.ToInt64():X}.");
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmClipboardUpdate)
            {
                _changed();
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (Handle != IntPtr.Zero && !RemoveClipboardFormatListener(Handle))
            {
                var exception = new Win32Exception(Marshal.GetLastWin32Error(), "Could not unregister clipboard update listener.");
                _logger.Warning($"Clipboard listener unregister failed. {exception.Message}");
            }

            DestroyHandle();
            _disposed = true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }

    private sealed class ClipboardChangeSubscription : IDisposable
    {
        private readonly Action _changed;
        private readonly Action<ClipboardChangeSubscription> _remove;
        private int _disposed;

        public ClipboardChangeSubscription(
            long id,
            Action changed,
            Action<ClipboardChangeSubscription> remove)
        {
            Id = id;
            _changed = changed;
            _remove = remove;
        }

        public long Id { get; }

        public void Notify()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _changed();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _remove(this);
        }

        public void MarkDisposed()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }
    }
}
