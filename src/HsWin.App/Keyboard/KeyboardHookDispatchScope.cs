using HsWin.Core.Logging;

namespace HsWin.App.Keyboard;

internal sealed class KeyboardHookDispatchScope : IDisposable
{
    private static readonly AsyncLocal<KeyboardHookDispatchScope?> CurrentScope = new();

    private readonly IRuntimeLogger _logger;
    private readonly KeyboardHookDispatchScope? _previousScope;
    private readonly string _sourceDescription;
    private readonly List<DeferredAction> _deferredActions = [];
    private bool _disposed;

    private KeyboardHookDispatchScope(IRuntimeLogger logger, string sourceDescription)
    {
        _logger = logger;
        _sourceDescription = sourceDescription;
        _previousScope = CurrentScope.Value;
        CurrentScope.Value = this;
    }

    public static IDisposable Enter(IRuntimeLogger logger, string sourceDescription)
    {
        return new KeyboardHookDispatchScope(logger, sourceDescription);
    }

    public static bool TryDefer(Action action, string description)
    {
        ArgumentNullException.ThrowIfNull(action);

        var scope = CurrentScope.Value;
        if (scope is null)
        {
            return false;
        }

        scope._deferredActions.Add(new DeferredAction(action, description));
        scope._logger.Info(
            $"Keyboard remap input deferred action='{description}' source={scope._sourceDescription} pending={scope._deferredActions.Count}.");
        return true;
    }

    public static int CurrentDeferredActionCount => CurrentScope.Value?._deferredActions.Count ?? 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CurrentScope.Value = _previousScope;

        if (_deferredActions.Count == 0)
        {
            return;
        }

        var actions = _deferredActions.ToArray();
        _logger.Info($"Keyboard remap dispatch completed source={_sourceDescription} deferredActions={actions.Length}; scheduling injected input.");
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
        }, (actions, _logger, _sourceDescription));
    }

    private sealed record DeferredAction(Action Action, string Description);
}
