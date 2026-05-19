using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Scripting;

namespace HsWin.Core.Tests;

public sealed class ScriptRuntimeTests
{
    [Fact]
    public void ReloadExposesHsAlertShowWithDefaults()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(presenter);

        runtime.Reload("""hs.alert.show("Loaded");""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Loaded", request.Text);
        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(2000, request.DurationMs);
    }

    [Fact]
    public void ReloadSupportsAlertObjectOptions()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(presenter);

        runtime.Reload("""hs.alert.show("Plain", { type: "normal", durationMs: 1250 });""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Plain", request.Text);
        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(1250, request.DurationMs);
    }

    [Fact]
    public void ReloadSupportsAlertKindAndDurationArguments()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(presenter);

        runtime.Reload("""hs.alert.show("Boom", "error", 4500);""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Boom", request.Text);
        Assert.Equal(AlertKind.Error, request.Kind);
        Assert.Equal(4500, request.DurationMs);
    }

    [Fact]
    public void ReloadCreatesFreshJavaScriptEngine()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(presenter);

        runtime.Reload("globalThis.previousValue = 42;");
        runtime.Reload("""hs.alert.show(String(globalThis.previousValue), { type: "normal", durationMs: 1 });""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("undefined", request.Text);
    }

    [Fact]
    public void ReloadRegistersHotkeyBindings()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys);

        runtime.Reload("""hs.hotkey.bind(["ctrl", "alt"], "R", () => hs.alert.show("Pressed"));""");
        hotkeys.TriggerOnlyRegistration();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Pressed", request.Text);
        var registration = Assert.Single(hotkeys.Registrations);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, registration.Hotkey.Modifiers);
        Assert.Equal((uint)'R', registration.Hotkey.VirtualKey);
    }

    [Fact]
    public void ReloadRegistersMouseHotkeyBindings()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys);

        runtime.Reload("""hs.hotkey.bind(["ctrl"], "mouse.back", () => hs.alert.show("Mouse"));""");
        hotkeys.TriggerOnlyRegistration();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Mouse", request.Text);
        var registration = Assert.Single(hotkeys.Registrations);
        Assert.Equal(HotkeyInputKind.MouseButton, registration.Hotkey.InputKind);
        Assert.Equal(HotkeyModifiers.Control, registration.Hotkey.Modifiers);
        Assert.Equal(HotkeyMouseButton.XButton1, registration.Hotkey.MouseButton);
    }

    [Fact]
    public void ReloadDisposesPreviousHotkeyBindings()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys);

        runtime.Reload("""hs.hotkey.bind(["ctrl"], "A", () => hs.alert.show("old"));""");
        var oldRegistration = Assert.Single(hotkeys.Registrations);
        runtime.Reload("""hs.hotkey.bind(["ctrl"], "B", () => hs.alert.show("new"));""");

        Assert.True(oldRegistration.IsDisposed);
        Assert.Equal(2, hotkeys.Registrations.Count);
        Assert.False(hotkeys.Registrations[1].IsDisposed);
    }

    [Fact]
    public void HotkeyCallbackErrorsShowAlertInsteadOfEscaping()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys);

        runtime.Reload("""hs.hotkey.bind(["ctrl"], "E", () => { throw new Error("boom"); });""");
        hotkeys.TriggerOnlyRegistration();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(AlertKind.Error, request.Kind);
        Assert.Contains("Hotkey callback error", request.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReloadWritesConsoleLogToReloadScopedLogFile()
    {
        using var directory = TemporaryDirectory.Create();
        var console = new ReloadScriptConsoleLogger(
            directory.Path,
            () => new DateTimeOffset(2026, 5, 19, 13, 47, 0, TimeSpan.Zero));
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys, console);

        runtime.Reload("""console.log("value", { count: 3 });""");

        var contents = File.ReadAllText(console.CurrentLogFilePath!);
        Assert.Contains("[log] value {\"count\":3}", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void ReloadRotatesConsoleLogFile()
    {
        using var directory = TemporaryDirectory.Create();
        var console = new ReloadScriptConsoleLogger(
            directory.Path,
            () => new DateTimeOffset(2026, 5, 19, 13, 47, 0, TimeSpan.Zero));
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(presenter, hotkeys, console);

        runtime.Reload("""console.log("first");""");
        var firstPath = console.CurrentLogFilePath;
        runtime.Reload("""console.log("second");""");
        var secondPath = console.CurrentLogFilePath;

        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal("05-19-2026-13-47-2.log", Path.GetFileName(secondPath));
    }

    [Fact]
    public void ReloadExposesApplicationIsRunning()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var applications = new CapturingApplicationProvider(
            new ApplicationSnapshot(123, "r5apex", "Apex Legends", @"C:\Games\Apex\r5apex.exe"));
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            applications,
            NullMediaController.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            if (hs.application.isRunning("r5apex.exe")) {
              hs.alert.show("Apex is running");
            }
            """);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Apex is running", request.Text);
    }

    [Fact]
    public void ReloadExposesRunningApplications()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var applications = new CapturingApplicationProvider(
            new ApplicationSnapshot(123, "chrome", "Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            new ApplicationSnapshot(456, "r5apex", "Apex Legends", @"C:\Games\Apex\r5apex.exe"));
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            applications,
            NullMediaController.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const apps = hs.application.runningApplications();
            const apex = apps.find(app => app.processName === "r5apex");
            hs.alert.show(`${apps.length}:${apex.pid}:${apex.path}`, "normal", 1);
            """);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(@"2:456:C:\Games\Apex\r5apex.exe", request.Text);
    }

    [Fact]
    public void ReloadExposesMediaControls()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var media = new CapturingMediaController();
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            media,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            hs.media.playPause();
            hs.media.previousTrack();
            hs.media.nextTrack();
            """);

        Assert.Equal(["playPause", "previousTrack", "nextTrack"], media.Actions);
    }

    [Fact]
    public void ReloadExposesMediaCommandResult()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var media = new CapturingMediaController
        {
            PlayPauseResult = new MediaCommandResult("playPause", true, "paused", "playing", "paused", "mediaSession")
        };
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            media,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const result = hs.media.playPause();
            hs.alert.show(`${result.action}:${result.statusBefore}:${result.statusAfter}:${result.success}`, "normal", 1);
            """);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("paused:playing:paused:true", request.Text);
    }

    [Fact]
    public void HotkeysCanTriggerMediaControls()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var applications = new CapturingApplicationProvider(
            new ApplicationSnapshot(123, "r5apex_dx12", "Apex Legends", null));
        var media = new CapturingMediaController();
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            applications,
            media,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const apexProcessName = "r5apex_dx12.exe";
            hs.hotkey.bind([], "`", () => {
              if (hs.application.isRunning(apexProcessName)) hs.media.playPause();
            });
            """);

        hotkeys.TriggerOnlyRegistration();

        Assert.Equal(["playPause"], media.Actions);
    }

    private sealed class CapturingAlertPresenter : IAlertPresenter
    {
        public List<AlertRequest> Requests { get; } = [];

        public void Show(AlertRequest request)
        {
            Requests.Add(request);
        }
    }

    private sealed class CapturingHotkeyRegistrar : IHotkeyRegistrar
    {
        public List<CapturingRegistration> Registrations { get; } = [];

        public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
        {
            var registration = new CapturingRegistration(hotkey, pressed);
            Registrations.Add(registration);
            return registration;
        }

        public void TriggerOnlyRegistration()
        {
            Assert.Single(Registrations).Trigger();
        }
    }

    private sealed class CapturingRegistration : IDisposable
    {
        private readonly Action _pressed;

        public CapturingRegistration(HotkeyDefinition hotkey, Action pressed)
        {
            Hotkey = hotkey;
            _pressed = pressed;
        }

        public HotkeyDefinition Hotkey { get; }

        public bool IsDisposed { get; private set; }

        public void Trigger()
        {
            _pressed();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class CapturingApplicationProvider : IApplicationProvider
    {
        private readonly IReadOnlyList<ApplicationSnapshot> _applications;

        public CapturingApplicationProvider(params ApplicationSnapshot[] applications)
        {
            _applications = applications;
        }

        public bool IsRunning(string processName)
        {
            var normalizedName = ProcessNameMatcher.Normalize(processName);
            return _applications.Any(application =>
                string.Equals(application.ProcessName, normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<ApplicationSnapshot> GetRunningApplications()
        {
            return _applications;
        }
    }

    private sealed class CapturingMediaController : IMediaController
    {
        public List<string> Actions { get; } = [];

        public MediaCommandResult PlayPauseResult { get; init; } =
            new("playPause", true, "played", "paused", "playing", "mediaSession");

        public MediaCommandResult PlayPause()
        {
            Actions.Add("playPause");
            return PlayPauseResult;
        }

        public MediaCommandResult PreviousTrack()
        {
            Actions.Add("previousTrack");
            return MediaCommandResult.Sent("previousTrack", "test");
        }

        public MediaCommandResult NextTrack()
        {
            Actions.Add("nextTrack");
            return MediaCommandResult.Sent("nextTrack", "test");
        }
    }
}
