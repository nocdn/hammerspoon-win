using System.Diagnostics;
using System.Globalization;
using HsWin.Core.Logging;
using HsWin.Core.Shell;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public sealed class TaskScriptApi
{
    private readonly IShellService _shell;
    private readonly IRuntimeLogger _logger;
    private readonly IScriptCallbackScheduler _callbackScheduler;
    private readonly ScriptCallbackInvoker _callbacks;
    private readonly Action<IDisposable> _trackResource;

    internal TaskScriptApi(
        IShellService shell,
        IRuntimeLogger logger,
        IScriptCallbackScheduler callbackScheduler,
        ScriptCallbackInvoker callbacks,
        Action<IDisposable> trackResource)
    {
        _shell = shell;
        _logger = logger;
        _callbackScheduler = callbackScheduler;
        _callbacks = callbacks;
        _trackResource = trackResource;
    }

    public ScriptResourceHandle Run(object? command, object? options, object? callback)
    {
        var normalizedCommand = ScriptArgumentReader.RequireNonWhiteSpaceString(command, "command");
        var parsedOptions = ShellScriptApi.ParseExecutionOptions(options);
        if (callback is not ScriptObject scriptFunction)
        {
            throw new ArgumentException("Task callback must be a JavaScript function.", nameof(callback));
        }

        var task = new BackgroundCommandTask(
            normalizedCommand,
            parsedOptions,
            scriptFunction,
            _shell,
            _logger,
            _callbackScheduler,
            _callbacks);
        var handle = new ScriptResourceHandle(task);
        _trackResource(handle);
        task.Start();
        _logger.Info($"Script hs.task.run() started command={LogSanitizer.DescribeCommand(normalizedCommand)} timeoutMs={parsedOptions.TimeoutMs}.");
        return handle;
    }

    private sealed class BackgroundCommandTask : IDisposable
    {
        private readonly string _command;
        private readonly ShellExecutionOptions _options;
        private readonly ScriptObject _callback;
        private readonly IShellService _shell;
        private readonly IRuntimeLogger _logger;
        private readonly IScriptCallbackScheduler _callbackScheduler;
        private readonly ScriptCallbackInvoker _callbacks;
        private int _disposed;

        public BackgroundCommandTask(
            string command,
            ShellExecutionOptions options,
            ScriptObject callback,
            IShellService shell,
            IRuntimeLogger logger,
            IScriptCallbackScheduler callbackScheduler,
            ScriptCallbackInvoker callbacks)
        {
            _command = command;
            _options = options;
            _callback = callback;
            _shell = shell;
            _logger = logger;
            _callbackScheduler = callbackScheduler;
            _callbacks = callbacks;
        }

        public void Start()
        {
            _ = System.Threading.Tasks.Task.Run(RunCommand);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }

        private void RunCommand()
        {
            var startedAt = Stopwatch.GetTimestamp();
            var result = _shell.Execute(_command, _options);
            _logger.Info(
                $"Script hs.task.run() command completed success={result.Success} exitCode={result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "null"} " +
                $"timedOut={result.TimedOut} elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");

            if (IsDisposed())
            {
                _logger.Info("Script hs.task.run() callback skipped because task was disposed.");
                return;
            }

            var resultJson = ScriptJson.Serialize(result);
            _callbackScheduler.Schedule(() =>
            {
                if (IsDisposed())
                {
                    _logger.Info("Script hs.task.run() scheduled callback skipped because task was disposed.");
                    return;
                }

                _callbacks.InvokeScriptCallback(_callback, resultJson);
            });
        }

        private bool IsDisposed()
        {
            return Volatile.Read(ref _disposed) != 0;
        }
    }
}
