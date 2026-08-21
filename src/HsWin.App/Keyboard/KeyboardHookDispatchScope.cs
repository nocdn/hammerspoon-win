using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Keyboard;

/// <summary>
/// Per-dispatch scope that lets input triggered synchronously by a blocking keyboard watcher
/// (for example a remap's injected tap) be deferred until after the hook callback returns.
/// Entered once per keystroke on the dedicated WH_KEYBOARD_LL thread; deferral is rare, so the
/// scope avoids allocating until a caller actually defers. The scope is tracked with a
/// thread-static field rather than AsyncLocal because every dispatch runs on that single hook
/// thread and ExecutionContext churn is measurable at keystroke rate.
/// </summary>
internal sealed class KeyboardHookDispatchScope : IDisposable
{
    [ThreadStatic]
    private static KeyboardHookDispatchScope? currentScope;

    private readonly IRuntimeLogger _logger;
    private readonly KeyboardEventSnapshot _snapshot;
    private readonly uint _scanCode;
    private readonly uint _flags;
    private readonly int _message;
    private readonly KeyboardHookDispatchScope? _previousScope;
    private List<DeferredAction>? _deferredActions;
    private bool _disposed;

    private KeyboardHookDispatchScope(
        IRuntimeLogger logger,
        KeyboardEventSnapshot snapshot,
        uint scanCode,
        uint flags,
        int message)
    {
        _logger = logger;
        _snapshot = snapshot;
        _scanCode = scanCode;
        _flags = flags;
        _message = message;
        _previousScope = currentScope;
        currentScope = this;
    }

    public static IDisposable Enter(
        IRuntimeLogger logger,
        KeyboardEventSnapshot snapshot,
        uint scanCode,
        uint flags,
        int message)
    {
        return new KeyboardHookDispatchScope(logger, snapshot, scanCode, flags, message);
    }

    public static bool TryDefer(Action action, string description)
    {
        ArgumentNullException.ThrowIfNull(action);

        var scope = currentScope;
        if (scope is null || scope._disposed)
        {
            return false;
        }

        scope._deferredActions ??= [];
        scope._deferredActions.Add(new DeferredAction(action, description));
        scope._logger.Info(
            $"Keyboard remap input deferred action='{description}' source={scope.FormatSource()} pending={scope._deferredActions.Count}.");
        return true;
    }

    public static int CurrentDeferredActionCount => currentScope?._deferredActions?.Count ?? 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        currentScope = _previousScope;

        if (_deferredActions is not { Count: > 0 })
        {
            return;
        }

        var actions = _deferredActions.ToArray();
        _logger.Info($"Keyboard remap dispatch completed source={FormatSource()} deferredActions={actions.Length}; scheduling injected input.");
        ThreadPool.QueueUserWorkItem(static state =>
        {
            var (deferredActions, logger, sourceDescription) =
                ((DeferredAction[] Actions, IRuntimeLogger Logger, string SourceDescription))state!;
            foreach (var deferredAction in deferredActions)
            {
                try
                {
                    logger.Info($"Keyboard remap deferred input executing action='{deferredAction.Description}' source={sourceDescription}.");
                    deferredAction.Action();
                    logger.Info($"Keyboard remap deferred input completed action='{deferredAction.Description}' source={sourceDescription}.");
                }
                catch (Exception exception)
                {
                    logger.Error($"Deferred keyboard hook action failed '{deferredAction.Description}'.", exception);
                }
            }
        }, (actions, _logger, FormatSource()));
    }

    private string FormatSource()
    {
        var snapshot = _snapshot;
        return
            $"key='{snapshot.Key}' type='{snapshot.Type}' vk=0x{snapshot.KeyCode:X2} scan=0x{_scanCode:X2} " +
            $"flags=0x{_flags:X2} message=0x{_message:X4} injected={snapshot.IsInjected} extended={snapshot.IsExtended}";
    }

    private sealed record DeferredAction(Action Action, string Description);
}
