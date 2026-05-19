using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Audio;
using HsWin.Core.Clipboard;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Shell;
using HsWin.Core.Timers;
using Microsoft.ClearScript.V8;

namespace HsWin.Core.Scripting;

public sealed class ScriptRuntime : IDisposable
{
    private static readonly string BootstrapScript = ScriptBootstrap.Load();

    private readonly ScriptRuntimeServices _services;
    private readonly List<IDisposable> _runtimeResources = [];
    private V8ScriptEngine? _engine;
    private bool _disposed;

    public ScriptRuntime()
        : this(new ScriptRuntimeServices())
    {
    }

    public ScriptRuntime(IAlertPresenter alerts)
        : this(new ScriptRuntimeServices { Alerts = alerts })
    {
    }

    public ScriptRuntime(IAlertPresenter alerts, IHotkeyRegistrar hotkeys)
        : this(new ScriptRuntimeServices { Alerts = alerts, Hotkeys = hotkeys })
    {
    }

    public ScriptRuntime(IAlertPresenter alerts, IHotkeyRegistrar hotkeys, IScriptConsoleLogger console)
        : this(new ScriptRuntimeServices { Alerts = alerts, Hotkeys = hotkeys, Console = console })
    {
    }

    public ScriptRuntime(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IRuntimeLogger logger)
        : this(new ScriptRuntimeServices
        {
            Alerts = alerts,
            Hotkeys = hotkeys,
            Console = console,
            Applications = applications,
            Media = media,
            Logger = logger
        })
    {
    }

    public ScriptRuntime(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IKeyboardEventService keyboardEvents,
        IKeyboardInputService keyboardInput,
        IScriptTimerService timers,
        IRuntimeLogger logger)
        : this(new ScriptRuntimeServices
        {
            Alerts = alerts,
            Hotkeys = hotkeys,
            Console = console,
            Applications = applications,
            Media = media,
            KeyboardEvents = keyboardEvents,
            KeyboardInput = keyboardInput,
            Timers = timers,
            Logger = logger
        })
    {
    }

    public ScriptRuntime(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IKeyboardEventService keyboardEvents,
        IKeyboardInputService keyboardInput,
        IScriptTimerService timers,
        IClipboardService clipboard,
        IShellService shell,
        IAudioDeviceController audioDevices,
        IRuntimeLogger logger)
        : this(new ScriptRuntimeServices
        {
            Alerts = alerts,
            Hotkeys = hotkeys,
            Console = console,
            Applications = applications,
            Media = media,
            KeyboardEvents = keyboardEvents,
            KeyboardInput = keyboardInput,
            Timers = timers,
            Clipboard = clipboard,
            Shell = shell,
            AudioDevices = audioDevices,
            Logger = logger
        })
    {
    }

    public ScriptRuntime(ScriptRuntimeServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public void ReloadFromFile(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Reload(File.ReadAllText(filePath), filePath);
    }

    public void Reload(string source, string documentName = "config.js")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        DisposeRuntimeResources();
        DisposeEngine();
        _services.Console.BeginReload(documentName);

        var engine = new V8ScriptEngine();
        var newRuntimeResources = new List<IDisposable>();
        try
        {
            engine.AddHostObject(
                "__hswinHost",
                new HsScriptHost(_services, newRuntimeResources.Add));
            engine.Execute("hswin:bootstrap", BootstrapScript);
            engine.Execute(documentName, source);
            _engine = engine;
            _runtimeResources.AddRange(newRuntimeResources);
        }
        catch
        {
            DisposeResources(newRuntimeResources);
            engine.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeRuntimeResources();
        DisposeEngine();
        _disposed = true;
    }

    private void DisposeRuntimeResources()
    {
        DisposeResources(_runtimeResources);
        _runtimeResources.Clear();
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    private static void DisposeResources(IEnumerable<IDisposable> resources)
    {
        foreach (var resource in resources.Reverse())
        {
            resource.Dispose();
        }
    }
}
