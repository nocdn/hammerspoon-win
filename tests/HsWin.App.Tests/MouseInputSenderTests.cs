using HsWin.App.Input;

namespace HsWin.App.Tests;

public sealed class MouseInputSenderTests
{
    [Fact]
    public void NativeInputSizeMatchesWin64SendInputLayout()
    {
        if (!Environment.Is64BitProcess)
        {
            return;
        }

        Assert.Equal(40, MouseInputSender.NativeInputSize);
    }
}
