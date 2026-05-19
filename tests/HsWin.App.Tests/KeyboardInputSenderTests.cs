using HsWin.App.Input;

namespace HsWin.App.Tests;

public sealed class KeyboardInputSenderTests
{
    [Fact]
    public void NativeInputSizeMatchesWin64SendInputLayout()
    {
        if (!Environment.Is64BitProcess)
        {
            return;
        }

        Assert.Equal(40, KeyboardInputSender.NativeInputSize);
    }
}
