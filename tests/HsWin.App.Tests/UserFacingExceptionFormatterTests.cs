using System.ComponentModel;
using System.Reflection;

namespace HsWin.App.Tests;

public sealed class UserFacingExceptionFormatterTests
{
    [Fact]
    public void FormatConfigReloadFailureUnwrapsTargetInvocationAndStripsScriptErrorPrefix()
    {
        var inner = new Win32Exception("Could not register hotkey Alt, Control+0x52.");
        var wrapped = new TargetInvocationException(inner);

        var message = UserFacingExceptionFormatter.FormatConfigReloadFailure(wrapped);

        Assert.Equal("Could not register hotkey Alt, Control+0x52.", message);
    }

    [Fact]
    public void FormatConfigReloadFailureStripsClearScriptErrorPrefix()
    {
        var exception = new Exception("Error: Could not register hotkey Alt, Control+0x52.");

        var message = UserFacingExceptionFormatter.FormatConfigReloadFailure(exception);

        Assert.Equal("Could not register hotkey Alt, Control+0x52.", message);
    }
}
