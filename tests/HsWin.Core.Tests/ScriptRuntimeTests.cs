using HsWin.Core.Alerts;
using HsWin.Core.Applications;
using HsWin.Core.Audio;
using HsWin.Core.Clipboard;
using HsWin.Core.Config;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;
using HsWin.Core.Media;
using HsWin.Core.Mouse;
using HsWin.Core.Scripting;
using HsWin.Core.Shell;
using HsWin.Core.Timers;

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
        private readonly Dictionary<string, AudioDeviceSnapshot> _devices;
        private readonly string _defaultDeviceId;

        public CapturingAudioDeviceController(params AudioDeviceSnapshot[] devices)
        {
            _devices = devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
            _defaultDeviceId = devices.Single(device => device.IsDefault).Id;
        }

        public List<string> Actions { get; } = [];

        public AudioDeviceSnapshot GetDefaultOutputDevice()
        {
            return _devices[_defaultDeviceId];
        }

        public IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices()
        {
            return _devices.Values.ToArray();
        }

        public AudioDeviceVolumeSnapshot GetVolume(string? deviceId)
        {
            return ToVolumeSnapshot(ResolveDevice(deviceId));
        }

        public AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume)
        {
            var device = ResolveDevice(deviceId);
            Actions.Add($"{device.Id}:{volume}");
            var updated = device with { Volume = volume };
            _devices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted)
        {
            var device = ResolveDevice(deviceId);
            Actions.Add($"{device.Id}:muted:{muted}");
            var updated = device with { Muted = muted };
            _devices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        public AudioDeviceVolumeSnapshot ToggleMute(string? deviceId)
        {
            var device = ResolveDevice(deviceId);
            Actions.Add($"{device.Id}:toggle");
            var updated = device with { Muted = !device.Muted };
            _devices[device.Id] = updated;
            return ToVolumeSnapshot(updated);
        }

        private AudioDeviceSnapshot ResolveDevice(string? deviceId)
        {
            return string.IsNullOrWhiteSpace(deviceId)
                ? _devices[_defaultDeviceId]
                : _devices[deviceId];
        }

        private static AudioDeviceVolumeSnapshot ToVolumeSnapshot(AudioDeviceSnapshot device)
        {
            return new AudioDeviceVolumeSnapshot(device.Id, device.Name, device.Volume, device.Muted);
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
