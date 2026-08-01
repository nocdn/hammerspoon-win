namespace HsWin.Core.Scripting;

public sealed class HsScriptHost
{
    public HsScriptHost(
        ScriptRuntimeServices services,
        Action<IDisposable> trackResource)
    {
        var callbacks = new ScriptCallbackInvoker(services.Alerts, services.Logger);

        Alerts = new AlertScriptApi(services.Alerts);
        Console = new ConsoleScriptApi(services.Console);
        Clipboard = new ClipboardScriptApi(
            services.Clipboard,
            services.Logger,
            services.CallbackScheduler,
            callbacks,
            trackResource);
        Shell = new ShellScriptApi(services.Shell, services.Logger);
        Applications = new ApplicationScriptApi(services.Applications, services.Shell, services.Logger);
        Media = new MediaScriptApi(services.Media, services.Logger);
        Audio = new AudioScriptApi(services.AudioDevices, services.Logger);
        AudioCapture = new AudioCaptureScriptApi(
            services.AudioCapture,
            services.Logger,
            services.CallbackScheduler,
            callbacks,
            trackResource);
        Mouse = new MouseScriptApi(services.Mouse, services.MouseInput, services.Logger, trackResource);
        Windows = new WindowScriptApi(
            services.Windows,
            services.Logger,
            services.CallbackScheduler,
            callbacks,
            trackResource);
        Http = new HttpScriptApi(
            services.Http,
            services.Logger,
            services.CallbackScheduler,
            callbacks,
            trackResource);
        Hotkeys = new HotkeyScriptApi(services.Hotkeys, services.KeyboardEvents, callbacks, trackResource);
        Tasks = new TaskScriptApi(
            services.Shell,
            services.Logger,
            services.CallbackScheduler,
            callbacks,
            trackResource);
        Keyboard = new KeyboardScriptApi(
            services.KeyboardEvents,
            services.KeyboardInput,
            services.Logger,
            callbacks,
            trackResource);
        Timers = new TimerScriptApi(services.Timers, services.Logger, callbacks, trackResource);
    }

    public AlertScriptApi Alerts { get; }

    public ConsoleScriptApi Console { get; }

    public ClipboardScriptApi Clipboard { get; }

    public ShellScriptApi Shell { get; }

    public ApplicationScriptApi Applications { get; }

    public MediaScriptApi Media { get; }

    public AudioScriptApi Audio { get; }

    public AudioCaptureScriptApi AudioCapture { get; }

    public MouseScriptApi Mouse { get; }

    public WindowScriptApi Windows { get; }

    public HttpScriptApi Http { get; }

    public HotkeyScriptApi Hotkeys { get; }

    public TaskScriptApi Tasks { get; }

    public KeyboardScriptApi Keyboard { get; }

    public TimerScriptApi Timers { get; }
}
