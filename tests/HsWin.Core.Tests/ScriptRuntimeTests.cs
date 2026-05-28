using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Audio;
using HsWin.Core.Clipboard;
using HsWin.Core.Config;
using HsWin.Core.Hotkeys;
using HsWin.Core.Http;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Mouse;
using HsWin.Core.Scripting;
using HsWin.Core.Shell;
using HsWin.Core.Timers;
using HsWin.Core.Windows;
using HsHttpRequestOptions = HsWin.Core.Http.HttpRequestOptions;

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
    public void ReloadSupportsAlertLoaderOption()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(presenter);

        runtime.Reload("""hs.alert.show("Working", { type: "normal", loading: true, durationMs: 60000 });""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Working", request.Text);
        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
        Assert.Equal(60000, request.DurationMs);
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
    public void ReloadAcceptsDefaultConfig()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload(ConfigFileService.DefaultConfig);

        Assert.NotEmpty(hotkeys.Registrations);
    }

    [Fact]
    public void ReloadAcceptsScriptRuntimeServices()
    {
        var presenter = new CapturingAlertPresenter();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter
        });

        runtime.Reload("""hs.alert.show("Services");""");

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("Services", request.Text);
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
    public void ReloadRegistersHeldHotkeyCallbacks()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            KeyboardEvents = keyboardEvents
        });

        runtime.Reload("""
            hs.hotkey.bindHeld(["ctrl", "alt"], "space", event => {
              hs.alert.show(`down:${event.key}`, "normal", 1);
            }, event => {
              hs.alert.show(`up:${event.key}`, "normal", 1);
            });
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.True(watch.Options.Blocking);
        Assert.False(watch.Options.IncludeInjected);

        var down = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            0x20,
            "space",
            ["ctrl", "alt"],
            (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt),
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: false));
        var repeat = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            0x20,
            "space",
            ["ctrl", "alt"],
            (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt),
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: false));
        var up = watch.Callback(new KeyboardEventSnapshot(
            "keyup",
            0x20,
            "space",
            ["ctrl", "alt"],
            (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt),
            IsKeyDown: false,
            IsKeyUp: true,
            IsModifier: false,
            IsInjected: false,
            IsExtended: false));

        Assert.True(down);
        Assert.True(repeat);
        Assert.True(up);
        Assert.Collection(
            presenter.Requests,
            request => Assert.Equal("down:space", request.Text),
            request => Assert.Equal("up:space", request.Text));
    }

    [Fact]
    public void HeldHotkeyReleasesWhenRequiredModifierIsReleased()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            KeyboardEvents = keyboardEvents
        });

        runtime.Reload("""
            hs.hotkey.whileHeld(["ctrl", "alt"], "R", () => hs.alert.show("start", "normal", 1), () => hs.alert.show("stop", "normal", 1));
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        _ = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            (uint)'R',
            "r",
            ["ctrl", "alt"],
            (uint)(HotkeyModifiers.Control | HotkeyModifiers.Alt),
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: false));
        var releasedModifier = watch.Callback(new KeyboardEventSnapshot(
            "keyup",
            KeyboardKeyRules.VkLeftMenu,
            "alt",
            ["ctrl"],
            (uint)HotkeyModifiers.Control,
            IsKeyDown: false,
            IsKeyUp: true,
            IsModifier: true,
            IsInjected: false,
            IsExtended: false));

        Assert.True(releasedModifier);
        Assert.Collection(
            presenter.Requests,
            request => Assert.Equal("start", request.Text),
            request => Assert.Equal("stop", request.Text));
    }

    [Fact]
    public void HeldHotkeyCanAllowExtraModifiersAndDisableBlocking()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            KeyboardEvents = keyboardEvents
        });

        runtime.Reload("""
            hs.hotkey.bindHeld(["ctrl"], "A", () => hs.alert.show("start", "normal", 1), () => hs.alert.show("stop", "normal", 1), {
              allowExtraModifiers: true,
              blocking: false,
              includeInjected: true
            });
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.False(watch.Options.Blocking);
        Assert.True(watch.Options.IncludeInjected);
        var swallowed = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            (uint)'A',
            "a",
            ["ctrl", "shift"],
            (uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift),
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: false));

        Assert.False(swallowed);
        Assert.Equal("start", Assert.Single(presenter.Requests).Text);
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
    public void ReloadDisposesPreviousHeldHotkeyBindings()
    {
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            KeyboardEvents = keyboardEvents
        });

        runtime.Reload("""hs.hotkey.bindHeld([], "F13", () => {}, () => {});""");
        var watch = Assert.Single(keyboardEvents.Watches).Registration;
        runtime.Reload("""console.log("new");""");

        Assert.True(watch.IsDisposed);
    }

    [Fact]
    public void HeldHotkeyRejectsMouseButtons()
    {
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            KeyboardEvents = new CapturingKeyboardEventService()
        });

        var exception = Assert.ThrowsAny<Exception>(() =>
            runtime.Reload("""hs.hotkey.bindHeld([], "mouse.middle", () => {}, () => {});"""));
        Assert.Contains("keyboard keys only", exception.Message, StringComparison.Ordinal);
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
    public void ReloadExposesPasteboardAndClipboardAliases()
    {
        var presenter = new CapturingAlertPresenter();
        var clipboard = new CapturingClipboardService("hello");
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            clipboard,
            NullShellService.Instance,
            NullAudioDeviceController.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const previous = hs.pasteboard.getContents();
            hs.clipboard.setContents(`${previous} world`);
            hs.alert.show(hs.pasteboard.getContents(), "normal", 1);
            """);

        Assert.Equal("hello world", clipboard.Text);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("hello world", request.Text);
    }

    [Fact]
    public void ReloadExposesExecuteCommand()
    {
        var presenter = new CapturingAlertPresenter();
        var shell = new CapturingShellService
        {
            ExecuteResult = new ShellExecutionResult("echo hello", true, 0, "hello\r\n", string.Empty, TimedOut: false)
        };
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullClipboardService.Instance,
            shell,
            NullAudioDeviceController.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const result = hs.execute("echo hello", { cwd: "C:\\Temp", timeoutMs: 1234 });
            hs.alert.show(`${result.success}:${result.status}:${result.exitCode}:${result.output.trim()}`, "normal", 1);
            """);

        var execution = Assert.Single(shell.Executions);
        Assert.Equal("echo hello", execution.Command);
        Assert.Equal(@"C:\Temp", execution.Options.WorkingDirectory);
        Assert.Equal(1234, execution.Options.TimeoutMs);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("true:true:0:hello", request.Text);
    }

    [Fact]
    public void ReloadExposesTaskRunForBackgroundCommands()
    {
        var presenter = new CapturingAlertPresenter();
        var shell = new CapturingShellService
        {
            ExecuteResult = new ShellExecutionResult("echo hello", true, 0, "hello\r\n", string.Empty, TimedOut: false)
        };
        var callbacks = new QueuedScriptCallbackScheduler();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Shell = shell,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            hs.alert.show("Working", { type: "normal", loading: true, durationMs: 60000 });
            hs.task.run("echo hello", { cwd: "C:\\Temp", timeoutMs: 1234 }, result => {
              hs.alert.show(`${result.success}:${result.exitCode}:${result.output.trim()}`, { type: "success", durationMs: 2500 });
            });
            """);

        var startAlert = Assert.Single(presenter.Requests);
        Assert.Equal("Working", startAlert.Text);
        Assert.Equal(AlertIcon.Loader, startAlert.EffectiveIcon);

        Assert.True(callbacks.WaitForCallback(TimeSpan.FromSeconds(5)));
        callbacks.RunNext();

        var execution = Assert.Single(shell.Executions);
        Assert.Equal("echo hello", execution.Command);
        Assert.Equal(@"C:\Temp", execution.Options.WorkingDirectory);
        Assert.Equal(1234, execution.Options.TimeoutMs);
        Assert.Collection(
            presenter.Requests,
            request => Assert.Equal("Working", request.Text),
            request =>
            {
                Assert.Equal("true:0:hello", request.Text);
                Assert.Equal(AlertKind.Success, request.Kind);
                Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
            });
    }

    [Fact]
    public void ReloadExposesHttpRequestWithMultipartFileUpload()
    {
        var presenter = new CapturingAlertPresenter();
        var callbacks = new QueuedScriptCallbackScheduler();
        var http = new CapturingHttpService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Http = http,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            hs.http.post("https://api.example.test/transcribe", {
              headers: { Authorization: "Bearer test-token" },
              multipart: [
                { name: "file", path: "C:\\Temp\\clip.wav", fileName: "clip.wav", contentType: "audio/wav" },
                { name: "model", value: "scribe-v1" }
              ],
              timeoutMs: 1234
            }, result => {
              hs.alert.show(`${result.success}:${result.statusCode}:${result.json.text}`, "normal", 1);
            });
            """);

        var request = Assert.Single(http.Requests);
        Assert.Equal("POST", request.Options.Method);
        Assert.Equal("https://api.example.test/transcribe", request.Options.Url);
        Assert.Equal("Bearer test-token", request.Options.Headers["Authorization"]);
        Assert.Equal(1234, request.Options.TimeoutMs);
        Assert.Collection(
            request.Options.Multipart,
            part =>
            {
                Assert.Equal("file", part.Name);
                Assert.Equal(@"C:\Temp\clip.wav", part.Path);
                Assert.Equal("clip.wav", part.FileName);
                Assert.Equal("audio/wav", part.ContentType);
            },
            part =>
            {
                Assert.Equal("model", part.Name);
                Assert.Equal("scribe-v1", part.Value);
            });

        request.Emit(new HttpResponseSnapshot(
            Success: true,
            StatusCode: 200,
            Status: "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body: """{"text":"hello world"}""",
            TimedOut: false,
            Error: null));
        callbacks.RunNext();

        var alert = Assert.Single(presenter.Requests);
        Assert.Equal("true:200:hello world", alert.Text);
    }

    [Fact]
    public void ReloadDisposesOutstandingHttpRequests()
    {
        var http = new CapturingHttpService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Http = http
        });

        runtime.Reload("""hs.http.get("https://example.test", () => {});""");
        var request = Assert.Single(http.Requests);
        runtime.Reload("""console.log("reloaded");""");

        Assert.True(request.IsDisposed);
    }

    [Fact]
    public void ReloadExposesOperationToastHandle()
    {
        var presenter = new CapturingAlertPresenter();
        var timers = new CapturingScriptTimerService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Timers = timers
        });

        runtime.Reload("""
            const toast = hs.alert.operation("Recording");
            toast.loading("Uploading");
            toast.loading("Transcribing");
            toast.success("Copied");
            """);

        Assert.Single(timers.Timers);
        Assert.Collection(
            presenter.Requests,
            request =>
            {
                Assert.StartsWith("Recording ", request.Text, StringComparison.Ordinal);
                Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
            },
            request =>
            {
                Assert.Equal("Uploading", request.Text);
                Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
            },
            request =>
            {
                Assert.Equal("Transcribing", request.Text);
                Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
            },
            request =>
            {
                Assert.Equal("Copied", request.Text);
                Assert.Equal(AlertKind.Success, request.Kind);
                Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
            });
        Assert.True(Assert.Single(timers.Timers).IsDisposed);
    }

    [Fact]
    public void ReloadDisposesOutstandingTaskRunCallbacks()
    {
        var presenter = new CapturingAlertPresenter();
        var shell = new CapturingShellService
        {
            ExecuteResult = new ShellExecutionResult("echo old", true, 0, "old\r\n", string.Empty, TimedOut: false)
        };
        var callbacks = new QueuedScriptCallbackScheduler();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Shell = shell,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            hs.task.run("echo old", result => {
              hs.alert.show("Old callback should not run");
            });
            """);

        Assert.True(callbacks.WaitForCallback(TimeSpan.FromSeconds(5)));
        runtime.Reload("""console.log("new config");""");
        callbacks.RunNext();

        Assert.Empty(presenter.Requests);
    }

    [Fact]
    public void ReloadExposesApplicationLaunch()
    {
        var presenter = new CapturingAlertPresenter();
        var shell = new CapturingShellService
        {
            LaunchResult = new LaunchResult("https://example.com", true, 42, null)
        };
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullClipboardService.Instance,
            shell,
            NullAudioDeviceController.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const result = hs.application.launch("https://example.com", { arguments: "--new-window", cwd: "C:\\Temp" });
            hs.alert.show(`${result.success}:${result.processId}`, "normal", 1);
            """);

        var launch = Assert.Single(shell.Launches);
        Assert.Equal("https://example.com", launch.Target);
        Assert.Equal("--new-window", launch.Options.Arguments);
        Assert.Equal(@"C:\Temp", launch.Options.WorkingDirectory);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("true:42", request.Text);
    }

    [Fact]
    public void ReloadExposesAudioDeviceAndSoundApis()
    {
        var presenter = new CapturingAlertPresenter();
        var audio = new CapturingAudioDeviceController(
            new AudioDeviceSnapshot("default-id", "Speakers", IsDefault: true, Volume: 25, Muted: false),
            new AudioDeviceSnapshot("headphones-id", "Headphones", IsDefault: false, Volume: 40, Muted: true));
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullClipboardService.Instance,
            NullShellService.Instance,
            audio,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            const device = hs.audiodevice.defaultOutputDevice();
            const devices = hs.audiodevice.allOutputDevices();
            device.setVolume(33);
            const mute = hs.sound.toggleMute();
            hs.alert.show(`${devices.length}:${device.name}:${hs.sound.getVolume()}:${mute.muted}`, "normal", 1);
            """);

        Assert.Equal(["default-id:33", "default-id:toggle"], audio.Actions);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("2:Speakers:33:true", request.Text);
    }

    [Fact]
    public void ReloadExposesAudioInputDeviceApis()
    {
        var presenter = new CapturingAlertPresenter();
        var audio = new CapturingAudioDeviceController(
            new AudioDeviceSnapshot("speakers-id", "Speakers", IsDefault: true, Volume: 25, Muted: false));
        audio.SetInputDevices(
            new AudioDeviceSnapshot("mic-id", "Studio Mic", IsDefault: true, Volume: 75, Muted: false),
            new AudioDeviceSnapshot("webcam-id", "Webcam Mic", IsDefault: false, Volume: 55, Muted: true));
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            AudioDevices = audio
        });

        runtime.Reload("""
            const mic = hs.audiodevice.defaultInputDevice();
            const inputs = hs.audiodevice.allInputDevices();
            mic.setVolume(62);
            const mute = mic.toggleMute();
            hs.alert.show(`${inputs.length}:${mic.kind}:${mic.name}:${hs.audiodevice.getInputVolume()}:${mute.muted}`, "normal", 1);
            """);

        Assert.Equal(["input:mic-id:62", "input:mic-id:toggle"], audio.Actions);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("2:input:Studio Mic:62:true", request.Text);
    }

    [Fact]
    public void ReloadExposesAudioRecordWithFriendlyOptionsAndCallbacks()
    {
        var presenter = new CapturingAlertPresenter();
        var callbacks = new QueuedScriptCallbackScheduler();
        var capture = new CapturingAudioCaptureService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            AudioCapture = capture,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            const recorder = hs.audio.record({
              path: "C:\\Temp\\voice.m4a",
              deviceId: "mic-id",
              quality: "high",
              levelIntervalMs: 0
            }, event => {
              if (event.type === "stopped") {
                hs.alert.show(`${event.path}:${event.format}:${event.bytes}`, "normal", 1);
              }
            });

            hs.alert.show(`${recorder.path}:${recorder.isRecording}`, "normal", 1);
            """);

        var recording = Assert.Single(capture.Recordings);
        Assert.Equal(@"C:\Temp\voice.m4a", recording.Options.Path);
        Assert.Equal("mic-id", recording.Options.DeviceId);
        Assert.Equal(AudioRecordingFormat.Aac, recording.Options.Format);
        Assert.Equal(256, recording.Options.BitrateKbps);
        Assert.Equal(0, recording.Options.LevelIntervalMs);

        recording.Emit(new AudioCaptureEvent("stopped", Path: @"C:\Temp\voice.m4a", Format: "m4a", Bytes: 1234));
        callbacks.RunNext();

        Assert.Collection(
            presenter.Requests,
            request => Assert.Equal(@"C:\Temp\voice.m4a:true", request.Text),
            request => Assert.Equal(@"C:\Temp\voice.m4a:m4a:1234", request.Text));
    }

    [Fact]
    public void ReloadDisposesAudioRecordingsOnReload()
    {
        var capture = new CapturingAudioCaptureService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            AudioCapture = capture
        });

        runtime.Reload("""hs.audio.record("C:\\Temp\\note.wav", () => {});""");
        var recording = Assert.Single(capture.Recordings);
        runtime.Reload("""console.log("reloaded");""");

        Assert.True(recording.IsDisposed);
    }

    [Fact]
    public void ReloadExposesAudioLevels()
    {
        var presenter = new CapturingAlertPresenter();
        var callbacks = new QueuedScriptCallbackScheduler();
        var capture = new CapturingAudioCaptureService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            AudioCapture = capture,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            hs.audio.levels({ deviceId: "mic-id", intervalMs: 75 }, event => {
              hs.alert.show(`${event.type}:${event.deviceId}:${event.peak}:${event.rms}`, "normal", 1);
            });
            """);

        var watch = Assert.Single(capture.LevelWatches);
        Assert.Equal("mic-id", watch.Options.DeviceId);
        Assert.Equal(75, watch.Options.IntervalMs);

        watch.Emit(new AudioCaptureEvent("level", DeviceId: "mic-id", Peak: 0.5, Rms: 0.25));
        callbacks.RunNext();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("level:mic-id:0.5:0.25", request.Text);
    }

    [Fact]
    public void ReloadExposesMouseCurrentScreen()
    {
        var presenter = new CapturingAlertPresenter();
        var mouse = new CapturingMouseService(
            new MouseScreenSnapshot(
                "display-2",
                "Right Monitor",
                IsPrimary: false,
                new MousePointSnapshot(1930, 50),
                new MouseRectangleSnapshot(1920, 0, 2560, 1440),
                new MouseRectangleSnapshot(1920, 0, 2560, 1400)));
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Mouse = mouse
        });

        runtime.Reload("""
            const screen = hs.mouse.getCurrentScreen();
            hs.alert.show(
              `${screen.id}:${screen.name}:${screen.isPrimary}:${screen.mousePosition.x},${screen.mousePosition.y}:${screen.bounds.width}x${screen.bounds.height}`,
              "normal",
              1);
            """);

        Assert.Equal(1, mouse.GetCurrentScreenCallCount);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("display-2:Right Monitor:false:1930,50:2560x1440", request.Text);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void ReloadExposesMousePrimaryScreenShortcut(bool isPrimary, string expectedText)
    {
        var presenter = new CapturingAlertPresenter();
        var mouse = new CapturingMouseService(
            new MouseScreenSnapshot(
                "display-1",
                "Primary Monitor",
                isPrimary,
                new MousePointSnapshot(10, 20),
                new MouseRectangleSnapshot(0, 0, 1920, 1080),
                new MouseRectangleSnapshot(0, 0, 1920, 1040)));
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Mouse = mouse
        });

        runtime.Reload("""
            hs.alert.show(String(hs.mouse.isOnPrimaryScreen()), "normal", 1);
            """);

        Assert.Equal(1, mouse.GetCurrentScreenCallCount);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal(expectedText, request.Text);
    }

    [Fact]
    public void ReloadExposesFocusedWindowSnapshot()
    {
        var presenter = new CapturingAlertPresenter();
        var windows = new CapturingWindowService
        {
            FocusedWindow = new WindowSnapshot(
                "0x1234",
                "Notes",
                42,
                "notepad",
                new WindowRectangleSnapshot(10, 20, 800, 600),
                IsMinimized: false,
                IsMaximized: false,
                IsVisible: true)
        };
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Windows = windows
        });

        runtime.Reload("""
            const win = hs.window.focusedWindow();
            hs.alert.show(`${win.id}:${win.title}:${win.processId}:${win.processName}:${win.frame.width}x${win.frame.height}`, "normal", 1);
            """);

        Assert.Equal(1, windows.GetFocusedWindowCallCount);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("0x1234:Notes:42:notepad:800x600", request.Text);
    }

    [Fact]
    public void WindowObjectMovesToMouseScreen()
    {
        var presenter = new CapturingAlertPresenter();
        var windows = new CapturingWindowService
        {
            FocusedWindow = new WindowSnapshot(
                "0x1234",
                "Notes",
                42,
                "notepad",
                new WindowRectangleSnapshot(10, 20, 800, 600),
                IsMinimized: false,
                IsMaximized: false,
                IsVisible: true),
            MoveResult = WindowMoveResult.MovedTo(
                "0x1234",
                new WindowRectangleSnapshot(1920, 0, 800, 600))
        };
        var mouse = new CapturingMouseService(
            new MouseScreenSnapshot(
                "display-2",
                "Right Monitor",
                IsPrimary: false,
                new MousePointSnapshot(1930, 50),
                new MouseRectangleSnapshot(1920, 0, 2560, 1440),
                new MouseRectangleSnapshot(1920, 0, 2560, 1400)));
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Mouse = mouse,
            Windows = windows
        });

        runtime.Reload("""
            const result = hs.window.focusedWindow().moveToMouseScreen({ preserveSize: true });
            hs.alert.show(`${result.success}:${result.moved}:${result.frame.x},${result.frame.y}`, "normal", 1);
            """);

        var move = Assert.Single(windows.Moves);
        Assert.Equal("0x1234", move.Id);
        Assert.Equal("display-2", move.TargetScreen.Id);
        Assert.True(move.Options.PreserveSize);
        Assert.True(move.Options.UseWorkingArea);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("true:true:1920,0", request.Text);
    }

    [Fact]
    public void WindowObjectCanSendNativeMonitorMoveToMouseScreen()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardInput = new CapturingKeyboardInputService();
        var windows = new CapturingWindowService
        {
            FocusedWindow = new WindowSnapshot(
                "0x1234",
                "Notes",
                42,
                "notepad",
                new WindowRectangleSnapshot(10, 20, 800, 600),
                IsMinimized: false,
                IsMaximized: true,
                IsVisible: true)
        };
        var mouse = new CapturingMouseService(
            new MouseScreenSnapshot(
                "display-2",
                "Right Monitor",
                IsPrimary: false,
                new MousePointSnapshot(1930, 50),
                new MouseRectangleSnapshot(1920, 0, 2560, 1440),
                new MouseRectangleSnapshot(1920, 0, 2560, 1400)));
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            KeyboardInput = keyboardInput,
            Mouse = mouse,
            Windows = windows
        });

        runtime.Reload("""
            const result = hs.window.focusedWindow().moveToMouseScreenNative();
            hs.alert.show(`${result.success}:${result.moved}:${result.reason}:${result.direction}`, "normal", 1);
            """);

        var tap = Assert.Single(keyboardInput.Taps);
        Assert.Equal(0x27u, tap.VirtualKey);
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Shift, tap.Options.Modifiers);
        var request = Assert.Single(presenter.Requests);
        Assert.Equal("true:true:sent-native-monitor-move:right", request.Text);
    }

    [Fact]
    public void WindowFocusWatchSchedulesCallbackAndDisposesOnReload()
    {
        var presenter = new CapturingAlertPresenter();
        var callbacks = new QueuedScriptCallbackScheduler();
        var windows = new CapturingWindowService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            Alerts = presenter,
            Windows = windows,
            CallbackScheduler = callbacks
        });

        runtime.Reload("""
            hs.window.watchFocused(win => {
              hs.alert.show(`${win.id}:${win.title}`, "normal", 1);
            });
            """);

        var watch = Assert.Single(windows.FocusWatches);
        watch.Emit(new WindowSnapshot(
            "0x9876",
            "Browser",
            100,
            "browser",
            new WindowRectangleSnapshot(0, 0, 1200, 800),
            IsMinimized: false,
            IsMaximized: true,
            IsVisible: true));
        callbacks.RunNext();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("0x9876:Browser", request.Text);

        runtime.Reload("""console.log("new config");""");

        Assert.True(watch.Registration.IsDisposed);
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

    [Fact]
    public void ReloadExposesKeyboardWatchAndTap()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var keyboardEvents = new CapturingKeyboardEventService();
        var keyboardInput = new CapturingKeyboardInputService();
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            keyboardInput,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            hs.keyboard.watch(event => {
              if (event.type === "keydown" && event.key === "w") {
                hs.keyboard.tap(event.keyCode, { suppressPhysicalModifiers: ["alt", "shift"] });
                return true;
              }

              return false;
            }, { blocking: true });
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.False(watch.Options.IncludeInjected);
        var swallowed = watch.Callback(
            new KeyboardEventSnapshot(
                "keydown",
                (uint)'W',
                "w",
                ["alt", "shift"],
                (uint)(HotkeyModifiers.Alt | HotkeyModifiers.Shift),
                IsKeyDown: true,
                IsKeyUp: false,
                IsModifier: false,
                IsInjected: false,
                IsExtended: false));

        Assert.True(swallowed);
        var tap = Assert.Single(keyboardInput.Taps);
        Assert.Equal((uint)'W', tap.VirtualKey);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, tap.Options.SuppressPhysicalModifiers);
    }

    [Fact]
    public void ReloadExposesKeyboardRemapWithoutScriptCallbackOnHookPath()
    {
        var keyboardEvents = new CapturingKeyboardEventService();
        var keyboardInput = new CapturingKeyboardInputService();
        var logger = new CapturingRuntimeLogger();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            KeyboardEvents = keyboardEvents,
            KeyboardInput = keyboardInput,
            Logger = logger
        });

        runtime.Reload("""hs.keyboard.remap("pageup", "end");""");

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.True(watch.Options.Blocking);
        Assert.False(watch.Options.IncludeInjected);
        Assert.True(watch.Options.Prepend);
        Assert.NotNull(watch.Options.KeyFilter);
        Assert.Contains(0x21u, watch.Options.KeyFilter);

        var ignored = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            0x22,
            "pagedown",
            [],
            0,
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: true));
        Assert.False(ignored);
        Assert.Empty(keyboardInput.Taps);

        var swallowedDown = watch.Callback(new KeyboardEventSnapshot(
            "keydown",
            0x21,
            "pageup",
            [],
            0,
            IsKeyDown: true,
            IsKeyUp: false,
            IsModifier: false,
            IsInjected: false,
            IsExtended: true));
        var swallowedUp = watch.Callback(new KeyboardEventSnapshot(
            "keyup",
            0x21,
            "pageup",
            [],
            0,
            IsKeyDown: false,
            IsKeyUp: true,
            IsModifier: false,
            IsInjected: false,
            IsExtended: true));

        Assert.True(swallowedDown);
        Assert.True(swallowedUp);
        var tap = Assert.Single(keyboardInput.Taps);
        Assert.Equal(0x23u, tap.VirtualKey);
        Assert.Contains(logger.Infos, info => info.Contains("hs.keyboard.remap('pageup', 'end')", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, info => info.Contains("Keyboard remap matched source='pageup'", StringComparison.Ordinal));
    }

    [Fact]
    public void ReloadExposesKeyboardTapModifiers()
    {
        var keyboardInput = new CapturingKeyboardInputService();
        using var runtime = new ScriptRuntime(new ScriptRuntimeServices
        {
            KeyboardInput = keyboardInput
        });

        runtime.Reload("""hs.keyboard.tap("right", { modifiers: ["win", "shift"] });""");

        var tap = Assert.Single(keyboardInput.Taps);
        Assert.Equal(0x27u, tap.VirtualKey);
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Shift, tap.Options.Modifiers);
    }

    [Fact]
    public void KeyboardWatchDefaultsToNonBlockingAndCannotSwallow()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardEvents = new CapturingKeyboardEventService();
        var logger = new CapturingRuntimeLogger();
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            logger);

        runtime.Reload("""
            hs.keyboard.watch(event => {
              hs.alert.show(event.key, "normal", 1);
              return true;
            });
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.False(watch.Options.Blocking);
        var swallowed = watch.Callback(
            new KeyboardEventSnapshot(
                "keydown",
                (uint)'A',
                "a",
                [],
                0,
                IsKeyDown: true,
                IsKeyUp: false,
                IsModifier: false,
                IsInjected: false,
                IsExtended: false));

        Assert.False(swallowed);
        Assert.Equal("a", Assert.Single(presenter.Requests).Text);
        Assert.Contains(logger.Warnings, warning => warning.Contains("Non-blocking hs.keyboard.watch", StringComparison.Ordinal));
    }

    [Fact]
    public void KeyboardWatchBlockingOptionCanSwallow()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            hs.keyboard.watch(() => true, { blocking: true });
            """);

        var watch = Assert.Single(keyboardEvents.Watches);
        Assert.True(watch.Options.Blocking);
        var swallowed = watch.Callback(
            new KeyboardEventSnapshot(
                "keydown",
                (uint)'A',
                "a",
                [],
                0,
                IsKeyDown: true,
                IsKeyUp: false,
                IsModifier: false,
                IsInjected: false,
                IsExtended: false));

        Assert.True(swallowed);
    }

    [Fact]
    public void KeyboardWatchParsesIncludeInjectedAndBlockingTogether()
    {
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(
            new CapturingAlertPresenter(),
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""hs.keyboard.watch(() => false, { includeInjected: true, blocking: true });""");

        var options = Assert.Single(keyboardEvents.Watches).Options;
        Assert.True(options.IncludeInjected);
        Assert.True(options.Blocking);
    }

    [Fact]
    public void KeyboardWatchParsesKeyFiltersFromJavaScript()
    {
        var keyboardEvents = new CapturingKeyboardEventService();
        using var runtime = new ScriptRuntime(
            new CapturingAlertPresenter(),
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            NullKeyboardInputService.Instance,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""hs.keyboard.watch(() => false, { blocking: true, keys: ["pageup", "pagedown", 0xC0] });""");

        var options = Assert.Single(keyboardEvents.Watches).Options;
        Assert.True(options.Blocking);
        Assert.NotNull(options.KeyFilter);
        Assert.Contains(0x21u, options.KeyFilter);
        Assert.Contains(0x22u, options.KeyFilter);
        Assert.Contains(0xC0u, options.KeyFilter);
    }

    [Fact]
    public void ReloadExposesKeyboardRepeat()
    {
        var presenter = new CapturingAlertPresenter();
        var keyboardInput = new CapturingKeyboardInputService();
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            keyboardInput,
            NullScriptTimerService.Instance,
            NullRuntimeLogger.Instance);

        runtime.Reload("""hs.keyboard.repeat("w", { intervalMs: 5, suppressPhysicalModifiers: ["alt", "shift"] });""");

        var repeat = Assert.Single(keyboardInput.Repeats);
        Assert.Equal((uint)'W', repeat.VirtualKey);
        Assert.Equal(5, repeat.Options.IntervalMs);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, repeat.Options.SuppressPhysicalModifiers);
    }

    [Fact]
    public void ReloadDisposesKeyboardWatchesAndTimers()
    {
        var presenter = new CapturingAlertPresenter();
        var hotkeys = new CapturingHotkeyRegistrar();
        var keyboardEvents = new CapturingKeyboardEventService();
        var timers = new CapturingScriptTimerService();
        using var runtime = new ScriptRuntime(
            presenter,
            hotkeys,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            keyboardEvents,
            NullKeyboardInputService.Instance,
            timers,
            NullRuntimeLogger.Instance);

        runtime.Reload("""
            hs.keyboard.watch(() => false);
            hs.timer.doEvery(25, () => {});
            """);
        var watch = Assert.Single(keyboardEvents.Watches).Registration;
        var timer = Assert.Single(timers.Timers);
        runtime.Reload("""console.log("second");""");

        Assert.True(watch.IsDisposed);
        Assert.True(timer.IsDisposed);
    }

    [Fact]
    public void ReloadExposesTimerDoAfter()
    {
        var presenter = new CapturingAlertPresenter();
        var timers = new CapturingScriptTimerService();
        using var runtime = new ScriptRuntime(
            presenter,
            NullHotkeyRegistrar.Instance,
            NullScriptConsoleLogger.Instance,
            NullApplicationProvider.Instance,
            NullMediaController.Instance,
            NullKeyboardEventService.Instance,
            NullKeyboardInputService.Instance,
            timers,
            NullRuntimeLogger.Instance);

        runtime.Reload("""hs.timer.doAfter(10, () => hs.alert.show("timer"));""");
        Assert.Single(timers.Timers).Trigger();

        var request = Assert.Single(presenter.Requests);
        Assert.Equal("timer", request.Text);
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

    private sealed class CapturingClipboardService : IClipboardService
    {
        public CapturingClipboardService(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }

        public string GetText()
        {
            return Text;
        }

        public bool SetText(string text)
        {
            Text = text;
            return true;
        }
    }

    private sealed class CapturingShellService : IShellService
    {
        public List<CapturingExecution> Executions { get; } = [];

        public List<CapturingLaunch> Launches { get; } = [];

        public ShellExecutionResult ExecuteResult { get; init; } =
            new("command", true, 0, string.Empty, string.Empty, TimedOut: false);

        public LaunchResult LaunchResult { get; init; } =
            new("target", true, 123, null);

        public ShellExecutionResult Execute(string command, ShellExecutionOptions options)
        {
            Executions.Add(new CapturingExecution(command, options));
            return ExecuteResult;
        }

        public LaunchResult Launch(string target, LaunchOptions options)
        {
            Launches.Add(new CapturingLaunch(target, options));
            return LaunchResult;
        }
    }

    private sealed record CapturingExecution(string Command, ShellExecutionOptions Options);

    private sealed record CapturingLaunch(string Target, LaunchOptions Options);

    private sealed class CapturingHttpService : IHttpService
    {
        public List<CapturingHttpRequest> Requests { get; } = [];

        public IDisposable Send(HsHttpRequestOptions options, Action<HttpResponseSnapshot> callback)
        {
            var request = new CapturingHttpRequest(options, callback);
            Requests.Add(request);
            return request;
        }
    }

    private sealed class CapturingHttpRequest : IDisposable
    {
        private readonly Action<HttpResponseSnapshot> _callback;

        public CapturingHttpRequest(HsHttpRequestOptions options, Action<HttpResponseSnapshot> callback)
        {
            Options = options;
            _callback = callback;
        }

        public HsHttpRequestOptions Options { get; }

        public bool IsDisposed { get; private set; }

        public void Emit(HttpResponseSnapshot response)
        {
            _callback(response);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class QueuedScriptCallbackScheduler : IScriptCallbackScheduler
    {
        private readonly Queue<Action> _callbacks = [];
        private readonly object _gate = new();
        private readonly ManualResetEventSlim _hasCallback = new();

        public void Schedule(Action callback)
        {
            lock (_gate)
            {
                _callbacks.Enqueue(callback);
                _hasCallback.Set();
            }
        }

        public bool WaitForCallback(TimeSpan timeout)
        {
            return _hasCallback.Wait(timeout);
        }

        public void RunNext()
        {
            Action callback;
            lock (_gate)
            {
                callback = _callbacks.Dequeue();
                if (_callbacks.Count == 0)
                {
                    _hasCallback.Reset();
                }
            }

            callback();
        }
    }

    private sealed class CapturingAudioDeviceController : IAudioDeviceController
    {
        private readonly Dictionary<string, AudioDeviceSnapshot> _outputDevices;
        private readonly Dictionary<string, AudioDeviceSnapshot> _inputDevices = new(StringComparer.OrdinalIgnoreCase);
        private string _defaultOutputDeviceId = string.Empty;
        private string _defaultInputDeviceId = string.Empty;

        public CapturingAudioDeviceController(params AudioDeviceSnapshot[] devices)
        {
            _outputDevices = devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
            _defaultOutputDeviceId = devices.Single(device => device.IsDefault).Id;
            SetInputDevices(devices);
        }

        public List<string> Actions { get; } = [];

        public void SetInputDevices(params AudioDeviceSnapshot[] devices)
        {
            _inputDevices.Clear();
            foreach (var device in devices)
            {
                _inputDevices.Add(device.Id, device);
            }

            _defaultInputDeviceId = devices.Single(device => device.IsDefault).Id;
        }

        public AudioDeviceSnapshot GetDefaultOutputDevice()
        {
            return _outputDevices[_defaultOutputDeviceId];
        }

        public IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices()
        {
            return _outputDevices.Values.ToArray();
        }

        public AudioDeviceSnapshot GetDefaultInputDevice()
        {
            return _inputDevices[_defaultInputDeviceId];
        }

        public IReadOnlyList<AudioDeviceSnapshot> GetInputDevices()
        {
            return _inputDevices.Values.ToArray();
        }

        public AudioDeviceVolumeSnapshot GetVolume(string? deviceId)
        {
            return ToVolumeSnapshot(ResolveOutputDevice(deviceId));
        }

        public AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume)
        {
            var device = ResolveOutputDevice(deviceId);
            Actions.Add($"{device.Id}:{volume}");
            var updated = device with { Volume = volume };
            _outputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted)
        {
            var device = ResolveOutputDevice(deviceId);
            Actions.Add($"{device.Id}:muted:{muted}");
            var updated = device with { Muted = muted };
            _outputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot ToggleMute(string? deviceId)
        {
            var device = ResolveOutputDevice(deviceId);
            Actions.Add($"{device.Id}:toggle");
            var updated = device with { Muted = !device.Muted };
            _outputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot GetInputVolume(string? deviceId)
        {
            return ToVolumeSnapshot(ResolveInputDevice(deviceId));
        }

        public AudioDeviceVolumeSnapshot SetInputVolume(string? deviceId, double volume)
        {
            var device = ResolveInputDevice(deviceId);
            Actions.Add($"input:{device.Id}:{volume}");
            var updated = device with { Volume = volume };
            _inputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot SetInputMuted(string? deviceId, bool muted)
        {
            var device = ResolveInputDevice(deviceId);
            Actions.Add($"input:{device.Id}:muted:{muted}");
            var updated = device with { Muted = muted };
            _inputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot ToggleInputMute(string? deviceId)
        {
            var device = ResolveInputDevice(deviceId);
            Actions.Add($"input:{device.Id}:toggle");
            var updated = device with { Muted = !device.Muted };
            _inputDevices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        private AudioDeviceSnapshot ResolveOutputDevice(string? deviceId)
        {
            return string.IsNullOrWhiteSpace(deviceId)
                ? _outputDevices[_defaultOutputDeviceId]
                : _outputDevices[deviceId];
        }

        private AudioDeviceSnapshot ResolveInputDevice(string? deviceId)
        {
            return string.IsNullOrWhiteSpace(deviceId)
                ? _inputDevices[_defaultInputDeviceId]
                : _inputDevices[deviceId];
        }

        private static AudioDeviceVolumeSnapshot ToVolumeSnapshot(AudioDeviceSnapshot device)
        {
            return new AudioDeviceVolumeSnapshot(device.Id, device.Name, device.Volume, device.Muted);
        }
    }

    private sealed class CapturingAudioCaptureService : IAudioCaptureService
    {
        public List<CapturingAudioRecording> Recordings { get; } = [];

        public List<CapturingAudioLevelWatch> LevelWatches { get; } = [];

        public IAudioRecordingSession Record(AudioRecordingOptions options, Action<AudioCaptureEvent> callback)
        {
            var recording = new CapturingAudioRecording(options, callback);
            Recordings.Add(recording);
            return recording;
        }

        public IDisposable WatchLevels(AudioLevelWatchOptions options, Action<AudioCaptureEvent> callback)
        {
            var watch = new CapturingAudioLevelWatch(options, callback);
            LevelWatches.Add(watch);
            return watch;
        }
    }

    private sealed class CapturingAudioRecording : IAudioRecordingSession
    {
        private readonly Action<AudioCaptureEvent> _callback;

        public CapturingAudioRecording(AudioRecordingOptions options, Action<AudioCaptureEvent> callback)
        {
            Options = options;
            _callback = callback;
        }

        public AudioRecordingOptions Options { get; }

        public string Path => Options.Path ?? @"C:\Users\Test\AppData\Roaming\HsWin\recordings\recording.wav";

        public bool IsRecording { get; private set; } = true;

        public bool IsDisposed { get; private set; }

        public void Emit(AudioCaptureEvent audioEvent)
        {
            _callback(audioEvent);
        }

        public void Stop()
        {
            IsRecording = false;
        }

        public void Dispose()
        {
            IsDisposed = true;
            Stop();
        }
    }

    private sealed class CapturingAudioLevelWatch : IDisposable
    {
        private readonly Action<AudioCaptureEvent> _callback;

        public CapturingAudioLevelWatch(AudioLevelWatchOptions options, Action<AudioCaptureEvent> callback)
        {
            Options = options;
            _callback = callback;
        }

        public AudioLevelWatchOptions Options { get; }

        public bool IsDisposed { get; private set; }

        public void Emit(AudioCaptureEvent audioEvent)
        {
            _callback(audioEvent);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class CapturingMouseService : IMouseService
    {
        private readonly MouseScreenSnapshot? _screen;

        public CapturingMouseService(MouseScreenSnapshot? screen)
        {
            _screen = screen;
        }

        public int GetCurrentScreenCallCount { get; private set; }

        public MouseScreenSnapshot? GetCurrentScreen()
        {
            GetCurrentScreenCallCount++;
            return _screen;
        }
    }

    private sealed class CapturingWindowService : IWindowService
    {
        public WindowSnapshot? FocusedWindow { get; init; }

        public WindowMoveResult MoveResult { get; init; } =
            WindowMoveResult.NotMoved("0x0", "not-configured");

        public int GetFocusedWindowCallCount { get; private set; }

        public List<CapturingWindowMove> Moves { get; } = [];

        public List<CapturingWindowFocusWatch> FocusWatches { get; } = [];

        public WindowSnapshot? GetFocusedWindow()
        {
            GetFocusedWindowCallCount++;
            return FocusedWindow;
        }

        public WindowSnapshot? GetWindow(string id)
        {
            return string.Equals(FocusedWindow?.Id, id, StringComparison.OrdinalIgnoreCase)
                ? FocusedWindow
                : null;
        }

        public WindowMoveResult MoveToScreen(string id, WindowTargetScreen targetScreen, WindowMoveOptions options)
        {
            Moves.Add(new CapturingWindowMove(id, targetScreen, options));
            return MoveResult with { WindowId = id };
        }

        public IDisposable WatchFocused(Action<WindowSnapshot> callback)
        {
            var registration = new CapturingDisposable();
            FocusWatches.Add(new CapturingWindowFocusWatch(callback, registration));
            return registration;
        }
    }

    private sealed record CapturingWindowMove(
        string Id,
        WindowTargetScreen TargetScreen,
        WindowMoveOptions Options);

    private sealed class CapturingWindowFocusWatch
    {
        private readonly Action<WindowSnapshot> _callback;

        public CapturingWindowFocusWatch(Action<WindowSnapshot> callback, CapturingDisposable registration)
        {
            _callback = callback;
            Registration = registration;
        }

        public CapturingDisposable Registration { get; }

        public void Emit(WindowSnapshot snapshot)
        {
            _callback(snapshot);
        }
    }

    private sealed class CapturingKeyboardEventService : IKeyboardEventService
    {
        public List<CapturingKeyboardWatch> Watches { get; } = [];

        public IDisposable Watch(KeyboardEventWatchOptions options, Func<KeyboardEventSnapshot, bool> callback)
        {
            var registration = new CapturingDisposable();
            Watches.Add(new CapturingKeyboardWatch(options, callback, registration));
            return registration;
        }

        public bool IsKeyDown(uint virtualKey)
        {
            return virtualKey == (uint)'W';
        }
    }

    private sealed record CapturingKeyboardWatch(
        KeyboardEventWatchOptions Options,
        Func<KeyboardEventSnapshot, bool> Callback,
        CapturingDisposable Registration);

    private sealed class CapturingKeyboardInputService : IKeyboardInputService
    {
        public List<CapturingTap> Taps { get; } = [];

        public List<CapturingRepeat> Repeats { get; } = [];

        public void KeyDown(uint virtualKey)
        {
        }

        public void KeyUp(uint virtualKey)
        {
        }

        public void Tap(uint virtualKey, KeyboardTapOptions options)
        {
            Taps.Add(new CapturingTap(virtualKey, options));
        }

        public IDisposable Repeat(uint virtualKey, KeyboardRepeatOptions options)
        {
            var registration = new CapturingDisposable();
            Repeats.Add(new CapturingRepeat(virtualKey, options, registration));
            return registration;
        }
    }

    private sealed record CapturingTap(uint VirtualKey, KeyboardTapOptions Options);

    private sealed record CapturingRepeat(
        uint VirtualKey,
        KeyboardRepeatOptions Options,
        CapturingDisposable Registration);

    private sealed class CapturingScriptTimerService : IScriptTimerService
    {
        public List<CapturingTimer> Timers { get; } = [];

        public IDisposable DoAfter(int delayMs, Action callback)
        {
            return AddTimer(delayMs, callback);
        }

        public IDisposable DoEvery(int intervalMs, Action callback)
        {
            return AddTimer(intervalMs, callback);
        }

        private CapturingTimer AddTimer(int intervalMs, Action callback)
        {
            var timer = new CapturingTimer(intervalMs, callback);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class CapturingTimer : IDisposable
    {
        private readonly Action _callback;

        public CapturingTimer(int intervalMs, Action callback)
        {
            IntervalMs = intervalMs;
            _callback = callback;
        }

        public int IntervalMs { get; }

        public bool IsDisposed { get; private set; }

        public void Trigger()
        {
            _callback();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class CapturingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class CapturingRuntimeLogger : IRuntimeLogger
    {
        public List<string> Infos { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Errors { get; } = [];

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add($"{message} {exception.Message}");
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
