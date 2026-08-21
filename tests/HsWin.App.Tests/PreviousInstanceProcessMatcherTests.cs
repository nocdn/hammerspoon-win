namespace HsWin.App.Tests;

public sealed class PreviousInstanceProcessMatcherTests
{
    [Fact]
    public void ShouldTerminateSkipsCurrentProcess()
    {
        var result = PreviousInstanceProcessMatcher.ShouldTerminate(
            candidateProcessId: 42,
            currentProcessId: 42,
            candidateProcessName: AppBranding.DisplayName,
            candidateSessionId: 1,
            currentSessionId: 1,
            candidateExecutablePath: null,
            currentExecutablePath: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTerminateMatchesInstalledAppProcessNameInCurrentSession()
    {
        var result = PreviousInstanceProcessMatcher.ShouldTerminate(
            candidateProcessId: 41,
            currentProcessId: 42,
            candidateProcessName: AppBranding.DisplayName,
            candidateSessionId: 1,
            currentSessionId: 1,
            candidateExecutablePath: null,
            currentExecutablePath: null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTerminateMatchesDevelopmentAppProcessNameInCurrentSession()
    {
        var result = PreviousInstanceProcessMatcher.ShouldTerminate(
            candidateProcessId: 41,
            currentProcessId: 42,
            candidateProcessName: "HsWin.App",
            candidateSessionId: 1,
            currentSessionId: 1,
            candidateExecutablePath: null,
            currentExecutablePath: null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTerminateSkipsKnownProcessNameInAnotherSession()
    {
        var result = PreviousInstanceProcessMatcher.ShouldTerminate(
            candidateProcessId: 41,
            currentProcessId: 42,
            candidateProcessName: AppBranding.DisplayName,
            candidateSessionId: 2,
            currentSessionId: 1,
            candidateExecutablePath: null,
            currentExecutablePath: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTerminateMatchesSameExecutablePath()
    {
        var result = PreviousInstanceProcessMatcher.ShouldTerminate(
            candidateProcessId: 41,
            currentProcessId: 42,
            candidateProcessName: "renamed",
            candidateSessionId: 1,
            currentSessionId: 1,
            candidateExecutablePath: @"C:\Program Files\HsWin\Hammerspoon (Windows Edition).exe",
            currentExecutablePath: @"C:\Program Files\HsWin\Hammerspoon (Windows Edition).exe");

        Assert.True(result);
    }

    [Fact]
    public void ShouldReadExecutablePathSkipsUnrelatedProcessNames()
    {
        var result = PreviousInstanceProcessMatcher.ShouldReadExecutablePath(
            "explorer",
            @"C:\Program Files\HsWin\Hammerspoon (Windows Edition).exe");

        Assert.False(result);
    }

    [Fact]
    public void ShouldReadExecutablePathMatchesCurrentExecutableProcessName()
    {
        var result = PreviousInstanceProcessMatcher.ShouldReadExecutablePath(
            "Hammerspoon (Windows Edition)",
            @"C:\Program Files\HsWin\Hammerspoon (Windows Edition).exe");

        Assert.True(result);
    }

    [Fact]
    public void CandidateProcessNamesIncludeKnownNamesWithoutDuplicates()
    {
        var names = PreviousInstanceProcessMatcher.CandidateProcessNames(
            @"C:\Program Files\Hammerspoon (Windows Edition)\Hammerspoon (Windows Edition).exe");

        Assert.Equal(2, names.Count);
        Assert.Contains(AppBranding.DisplayName, names);
        Assert.Contains("HsWin.App", names);
    }

    [Fact]
    public void CandidateProcessNamesAddCurrentExecutableBaseNameWhenDistinct()
    {
        var names = PreviousInstanceProcessMatcher.CandidateProcessNames(@"C:\devin\CustomBuild.exe");

        Assert.Equal(3, names.Count);
        Assert.Contains("CustomBuild", names);
    }

    [Fact]
    public void CandidateProcessNamesHandleMissingExecutablePath()
    {
        var names = PreviousInstanceProcessMatcher.CandidateProcessNames(null);

        Assert.Equal(2, names.Count);
    }
}
