using System.Globalization;
using HsWin.Core.Applications;
using HsWin.Core.Logging;
using HsWin.Core.Shell;

namespace HsWin.Core.Scripting;

public sealed class ApplicationScriptApi
{
    private readonly IApplicationProvider _applications;
    private readonly IShellService _shell;
    private readonly IRuntimeLogger _logger;

    public ApplicationScriptApi(
        IApplicationProvider applications,
        IShellService shell,
        IRuntimeLogger logger)
    {
        _applications = applications;
        _shell = shell;
        _logger = logger;
    }

    public bool IsRunning(object? processName)
    {
        var normalizedProcessName = ScriptArgumentReader.RequireNonWhiteSpaceString(processName, "processName");
        var result = _applications.IsRunning(normalizedProcessName);
        _logger.Info($"Script hs.application.isRunning('{normalizedProcessName}') returned {result}.");
        return result;
    }

    public string GetRunningApplicationsJson()
    {
        var applications = _applications.GetRunningApplications();
        _logger.Info($"Script hs.application.runningApplications() returned {applications.Count} processes.");
        return ScriptJson.Serialize(applications);
    }

    public string LaunchJson(object? target, object? options = null)
    {
        var normalizedTarget = ScriptArgumentReader.RequireNonWhiteSpaceString(target, "target");
        var parsedOptions = ShellScriptApi.ParseLaunchOptions(options);
        _logger.Info($"Script hs.application.launch() requested target='{normalizedTarget}'.");
        var result = _shell.Launch(normalizedTarget, parsedOptions);
        _logger.Info($"Script hs.application.launch() completed success={result.Success} processId={result.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "null"}.");
        return ScriptJson.Serialize(result);
    }
}
