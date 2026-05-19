using HammerspoonWin.Core.Alerts;
using HammerspoonWin.Core.Applications;
using HammerspoonWin.Core.Config;
using HammerspoonWin.Core.Logging;
using HammerspoonWin.Core.Scripting;
using HammerspoonWin.App.Hotkeys;
using HammerspoonWin.App.Media;
using System.Reflection;
using WpfApplication = System.Windows.Application;

namespace HammerspoonWin.App;

internal sealed class AppController : IDisposable
{
    private readonly ConfigFileService _configFileService;
    private readonly FileLogger _logger;
    private readonly ReloadScriptConsoleLogger _scriptConsoleLogger;
    private readonly ToastPresenter _toastPresenter;
    private readonly NativeHotkeyService _hotkeyService;
    private readonly ScriptRuntime _scriptRuntime;
    private readonly StartupService _startupService;
    private readonly TrayIconService _trayIconService;

    private bool _disposed;

    public AppController()
    {
        var paths = HammerspoonWinPaths.FromAppData();
        _configFileService = new ConfigFileService(paths.ConfigFilePath);
        _logger = FileLogger.CreateForLaunch(paths.RuntimeLogDirectory);
        _scriptConsoleLogger = new ReloadScriptConsoleLogger(paths.ConfigLogDirectory);
        _toastPresenter = new ToastPresenter();
        _hotkeyService = new NativeHotkeyService(_logger);
        _scriptRuntime = new ScriptRuntime(
            _toastPresenter,
            _hotkeyService,
            _scriptConsoleLogger,
            new ProcessApplicationProvider(_logger),
            new NativeMediaController(_logger),
            _logger);
        _startupService = new StartupService("HammerspoonWin", ResolveExecutablePath());
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
            _logger.Info("Starting HammerspoonWin.");
            _logger.Info($"Runtime log: {_logger.LogFilePath}");
            _logger.Info($"Config file: {_configFileService.ConfigFilePath}");
            _configFileService.EnsureConfigFile();
            _trayIconService.Show();
            _logger.Info("Tray icon shown.");
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
        try
        {
            _logger.Info("Reload Config requested.");
            _configFileService.EnsureConfigFile();
            _scriptRuntime.ReloadFromFile(_configFileService.ConfigFilePath);
            _logger.Info($"Config reloaded. Console log: {_scriptConsoleLogger.CurrentLogFilePath}");
        }
        catch (Exception exception)
        {
            _logger.Error("Config reload failed.", exception);
            _toastPresenter.Show(AlertRequest.Create($"Config error: {exception.Message}", AlertKind.Error, 7000));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _trayIconService.Dispose();
        _logger.Info("Tray icon disposed.");
        _scriptRuntime.Dispose();
        _logger.Info("Script runtime disposed.");
        _hotkeyService.Dispose();
        _logger.Info("Hotkey service disposed.");
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

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
            ?? Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("Could not resolve the current executable path.");
    }
}
