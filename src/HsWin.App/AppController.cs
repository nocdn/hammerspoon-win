using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Config;
using HsWin.Core.Logging;
using HsWin.Core.Scripting;
using HsWin.App.Audio;
using HsWin.App.Clipboard;
using HsWin.App.Hotkeys;
using HsWin.App.Input;
using HsWin.App.Keyboard;
using HsWin.App.Media;
using HsWin.App.Shell;
using HsWin.App.Timers;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace HsWin.App;

internal sealed class AppController : IDisposable
{
    private readonly ConfigFileService _configFileService;
    private readonly FileLogger _logger;
    private readonly SingleInstanceGuard _singleInstanceGuard;
    private readonly ReloadScriptConsoleLogger _scriptConsoleLogger;
    private readonly ToastPresenter _toastPresenter;
    private readonly NativeHotkeyService _hotkeyService;
    private readonly NativeKeyboardEventService _keyboardEventService;
    private readonly KeyboardInputService _keyboardInputService;
    private readonly DispatcherScriptTimerService _timerService;
    private readonly NativeClipboardService _clipboardService;
    private readonly ProcessShellService _shellService;
    private readonly NativeAudioDeviceController _audioDeviceController;
    private readonly ScriptRuntime _scriptRuntime;
    private readonly StartupService _startupService;
    private readonly TrayIconService _trayIconService;
    private readonly Dispatcher _dispatcher;
    private readonly object _scriptReloadGate = new();

    private int _reloadGeneration;
    private bool _disposed;

    public AppController()
    {
        var paths = HsWinPaths.FromAppData();
        _configFileService = new ConfigFileService(paths.ConfigFilePath);
        _logger = FileLogger.CreateForLaunch(paths.RuntimeLogDirectory);
        _singleInstanceGuard = SingleInstanceGuard.Acquire(_logger);
        _scriptConsoleLogger = new ReloadScriptConsoleLogger(paths.ConfigLogDirectory);
        _toastPresenter = new ToastPresenter(_logger);
        _hotkeyService = new NativeHotkeyService(_logger);
        _keyboardEventService = new NativeKeyboardEventService(_logger);
        _keyboardInputService = new KeyboardInputService(_logger);
        _timerService = new DispatcherScriptTimerService(WpfApplication.Current.Dispatcher);
        _clipboardService = new NativeClipboardService(WpfApplication.Current.Dispatcher, _logger);
        _shellService = new ProcessShellService(_logger);
        _audioDeviceController = new NativeAudioDeviceController(_logger);
        _scriptRuntime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = _toastPresenter,
            Hotkeys = _hotkeyService,
            Console = _scriptConsoleLogger,
            Applications = new ProcessApplicationProvider(_logger),
            Media = new NativeMediaController(_logger),
            KeyboardEvents = _keyboardEventService,
            KeyboardInput = _keyboardInputService,
            Timers = _timerService,
            Clipboard = _clipboardService,
            Shell = _shellService,
            AudioDevices = _audioDeviceController,
            Logger = _logger
        });
        _startupService = new StartupService(AppBranding.DisplayName, ResolveExecutablePath(), "HsWin");
        _dispatcher = WpfApplication.Current.Dispatcher;
        _trayIconService = new TrayIconService(
            openConfig: OpenConfig,
            reloadConfig: ReloadConfig,
            isStartAtLoginEnabled: _startupService.IsEnabled,
            setStartAtLoginEnabled: SetStartAtLoginEnabled,
            quit: Quit);
    }

    public void Start()
    {
        try
        {
            _logger.Info($"Starting {AppBranding.DisplayName}.");
            _logger.Info($"Runtime log: {_logger.LogFilePath}");
            _logger.Info($"Config file: {_configFileService.ConfigFilePath}");
            _configFileService.EnsureConfigFile();
            _trayIconService.Show();
            _logger.Info("Tray icon shown.");
            _toastPresenter.Prewarm();
            _logger.Info("Toast presenter prewarmed.");
            ReloadConfig();
        }
        catch (Exception exception)
        {
            _logger.Error("Startup failed.", exception);
            _toastPresenter.Show(AlertRequest.Create($"Startup failed: {exception.Message}", AlertKind.Error, 6000));
        }
    }

    public void OpenConfig()
    {
        try
        {
            _logger.Info("Open Config requested.");
            _configFileService.EnsureConfigFile();
            EditorLauncher.Open(_configFileService.ConfigFilePath);
            _logger.Info("Open Config completed.");
        }
        catch (Exception exception)
        {
            _logger.Error("Open config failed.", exception);
            _toastPresenter.Show(AlertRequest.Create($"Could not open config: {exception.Message}", AlertKind.Error, 6000));
        }
    }

    public void ReloadConfig()
    {
        var generation = Interlocked.Increment(ref _reloadGeneration);
        var startedAt = Stopwatch.GetTimestamp();
        _logger.Info("Reload Config requested.");
        _toastPresenter.Show(ConfigReloadAlerts.CreateReloadingAlert());

        Task.Run(() =>
        {
            Exception? failure = null;
            var superseded = false;
            try
            {
                _configFileService.EnsureConfigFile();
                var configPath = _configFileService.ConfigFilePath;
                lock (_scriptReloadGate)
                {
                    if (generation != Volatile.Read(ref _reloadGeneration) || _disposed)
                    {
                        superseded = true;
                    }
                    else
                    {
                        _scriptRuntime.ReloadFromFile(configPath);
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (superseded)
            {
                return;
            }

            var remainingReloadingTime = TimeSpan.FromMilliseconds(ConfigReloadAlerts.MinimumReloadingVisibleMs) - Stopwatch.GetElapsedTime(startedAt);
            if (remainingReloadingTime > TimeSpan.Zero)
            {
                Thread.Sleep(remainingReloadingTime);
            }

            _dispatcher.BeginInvoke(() => CompleteReloadOnDispatcher(generation, failure));
        });
    }

    private void CompleteReloadOnDispatcher(int generation, Exception? failure)
    {
        if (_disposed || generation != Volatile.Read(ref _reloadGeneration))
        {
            return;
        }

        if (failure is not null)
        {
            _logger.Error("Config reload failed.", failure);
            _toastPresenter.Show(ConfigReloadAlerts.CreateReloadFailedAlert(failure));
            return;
        }

        _logger.Info($"Config reloaded. Console log: {_scriptConsoleLogger.CurrentLogFilePath}");
        _toastPresenter.Show(ConfigReloadAlerts.CreateReloadedAlert());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Increment(ref _reloadGeneration);

        _trayIconService.Dispose();
        _logger.Info("Tray icon disposed.");

        lock (_scriptReloadGate)
        {
            _scriptRuntime.Dispose();
        }

        _logger.Info("Script runtime disposed.");
        _hotkeyService.Dispose();
        _logger.Info("Hotkey service disposed.");
        _keyboardEventService.Dispose();
        _logger.Info("Keyboard event service disposed.");
        _toastPresenter.Dispose();
        _logger.Info("Toast presenter disposed.");
        _singleInstanceGuard.Dispose();
        _logger.Info("Single instance guard disposed.");
        _disposed = true;
    }

    private void SetStartAtLoginEnabled(bool enabled)
    {
        try
        {
            _startupService.SetEnabled(enabled);
            var state = enabled ? "enabled" : "disabled";
            _logger.Info($"Start at login {state}.");
            _toastPresenter.Show(AlertRequest.Create($"Start at login {state}.", AlertKind.Success, 2000));
        }
        catch (Exception exception)
        {
            _logger.Error("Updating start at login failed.", exception);
            _toastPresenter.Show(AlertRequest.Create($"Could not update start at login: {exception.Message}", AlertKind.Error, 6000));
        }
    }

    private static void Quit()
    {
        WpfApplication.Current.Shutdown();
    }

    internal static AlertRequest CreateConfigReloadedAlert() => ConfigReloadAlerts.CreateReloadedAlert();

    internal static AlertRequest CreateConfigReloadingAlert() => ConfigReloadAlerts.CreateReloadingAlert();

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
            ?? Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("Could not resolve the current executable path.");
    }
}
