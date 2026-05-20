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

namespace HsWin.Core.Scripting;

public sealed record ScriptRuntimeServices
{
    public IAlertPresenter Alerts { get; init; } = NullAlertPresenter.Instance;

    public IHotkeyRegistrar Hotkeys { get; init; } = NullHotkeyRegistrar.Instance;

    public IScriptConsoleLogger Console { get; init; } = NullScriptConsoleLogger.Instance;

    public IApplicationProvider Applications { get; init; } = NullApplicationProvider.Instance;

    public IMediaController Media { get; init; } = NullMediaController.Instance;

    public IKeyboardEventService KeyboardEvents { get; init; } = NullKeyboardEventService.Instance;

    public IKeyboardInputService KeyboardInput { get; init; } = NullKeyboardInputService.Instance;

    public IScriptTimerService Timers { get; init; } = NullScriptTimerService.Instance;

    public IScriptCallbackScheduler CallbackScheduler { get; init; } = InlineScriptCallbackScheduler.Instance;

    public IClipboardService Clipboard { get; init; } = NullClipboardService.Instance;

    public IShellService Shell { get; init; } = NullShellService.Instance;

    public IAudioDeviceController AudioDevices { get; init; } = NullAudioDeviceController.Instance;

    public IRuntimeLogger Logger { get; init; } = NullRuntimeLogger.Instance;
}
