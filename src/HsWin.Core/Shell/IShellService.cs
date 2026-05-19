namespace HsWin.Core.Shell;

public interface IShellService
{
    ShellExecutionResult Execute(string command, ShellExecutionOptions options);

    LaunchResult Launch(string target, LaunchOptions options);
}
