using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Timers;
using Microsoft.ClearScript.V8;

namespace HsWin.Core.Scripting;

public sealed class ScriptRuntime : IDisposable
{
    private const string BootstrapScript = """
        (() => {
          const host = globalThis.__hswinHost;
          const formatConsoleValue = (value) => {
            if (typeof value === "string") {
              return value;
            }

            if (value instanceof Error) {
              return value.stack || value.message || String(value);
            }

            try {
              const serialized = JSON.stringify(value);
              if (serialized !== undefined) {
                return serialized;
              }
            } catch {
            }

            return String(value);
          };

          const writeConsole = (level, values) => {
            host.LogConsole(level, values.map(formatConsoleValue).join(" "));
          };

          globalThis.hs = Object.freeze({
            alert: Object.freeze({
              show(text, optionsOrKind, durationMs) {
                host.ShowAlert(text, optionsOrKind, durationMs);
              }
            }),

            hotkey: Object.freeze({
              bind(modifiers, key, pressedFn) {
                return host.BindHotkey(modifiers, key, pressedFn);
              }
            }),

            application: Object.freeze({
              isRunning(processName) {
                return host.IsApplicationRunning(processName);
              },

              runningApplications() {
                return JSON.parse(host.GetRunningApplicationsJson());
              }
            }),

            media: Object.freeze({
              playPause() {
                return JSON.parse(host.MediaPlayPauseJson());
              },

              previousTrack() {
                return JSON.parse(host.MediaPreviousTrackJson());
              },

              nextTrack() {
                return JSON.parse(host.MediaNextTrackJson());
              }
            }),

            keyboard: Object.freeze({
              watch(callback, options) {
                if (typeof callback !== "function") {
                  throw new Error("Keyboard watch callback must be a function.");
                }

                return host.WatchKeyboard((eventJson) => callback(JSON.parse(eventJson)) === true, options);
              },

              tap(key, options) {
                host.KeyboardTap(key, options);
              },

              repeat(key, options) {
                return host.KeyboardRepeat(key, options);
              },

              keyDown(key) {
                host.KeyboardKeyDown(key);
              },

              keyUp(key) {
                host.KeyboardKeyUp(key);
              },

              isDown(key) {
                return host.KeyboardIsDown(key);
              }
            }),

            timer: Object.freeze({
              doAfter(delayMs, callback) {
                if (typeof callback !== "function") {
                  throw new Error("Timer callback must be a function.");
                }

                return host.TimerDoAfter(delayMs, callback);
              },

              doEvery(intervalMs, callback) {
                if (typeof callback !== "function") {
                  throw new Error("Timer callback must be a function.");
                }

                return host.TimerDoEvery(intervalMs, callback);
              }
            })
          });

          globalThis.console = Object.freeze({
            log(...values) {
              writeConsole("log", values);
            },

            info(...values) {
              writeConsole("info", values);
            },

            warn(...values) {
              writeConsole("warn", values);
            },

            error(...values) {
              writeConsole("error", values);
            }
          });
        })();
        """;

    private readonly IAlertPresenter _alerts;
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IScriptConsoleLogger _console;
    private readonly IApplicationProvider _applications;
    private readonly IMediaController _media;
    private readonly IKeyboardEventService _keyboardEvents;
    private readonly IKeyboardInputService _keyboardInput;
    private readonly IScriptTimerService _timers;
    private readonly IRuntimeLogger _logger;
    private readonly List<IDisposable> _runtimeResources = [];
    private V8ScriptEngine? _engine;
    private bool _disposed;

    public ScriptRuntime(IAlertPresenter alerts)
        : this(
            alerts,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance)
    {
    }

    public ScriptRuntime(IAlertPresenter alerts, IHotkeyRegistrar hotkeys)
        : this(
            alerts,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance)
    {
    }

    public ScriptRuntime(IAlertPresenter alerts, IHotkeyRegistrar hotkeys, IScriptConsoleLogger console)
        : this(
            alerts,
            hotkeys,
            console,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance)
    {
    }

    public ScriptRuntime(
        IAlertPresenter alerts,
        IHotkeyRegistrar hotkeys,
        IScriptConsoleLogger console,
        IApplicationProvider applications,
        IMediaController media,
        IRuntimeLogger logger)
        : this(
            alerts,
            hotkeys,
            console,
            applications,
            media,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            logger)
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
    {
        _alerts = alerts;
        _hotkeys = hotkeys;
        _console = console;
        _applications = applications;
        _media = media;
        _keyboardEvents = keyboardEvents;
        _keyboardInput = keyboardInput;
        _timers = timers;
        _logger = logger;
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
        _console.BeginReload(documentName);

        var engine = new V8ScriptEngine();
        var newRuntimeResources = new List<IDisposable>();
        try
        {
            engine.AddHostObject(
                "__hswinHost",
                new HsScriptHost(
                    _alerts,
                    _hotkeys,
                    _console,
                    _applications,
                    _media,
                    _keyboardEvents,
                    _keyboardInput,
                    _timers,
                    _logger,
                    newRuntimeResources.Add));
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
