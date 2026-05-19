using HammerspoonWin.Core.Alerts;
using HammerspoonWin.Core.Hotkeys;
using HammerspoonWin.Core.Logging;
using HammerspoonWin.Core.Scripting;

namespace HammerspoonWin.Core.Tests;

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
}
