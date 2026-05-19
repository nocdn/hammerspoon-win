using HsWin.App.Media;

namespace HsWin.App.Tests;

public sealed class NativeMediaControllerTests
{
    [Fact]
    public void NativeInputSizeMatchesWin64SendInputLayout()
    {
        if (!Environment.Is64BitProcess)
        {
            return;
        }

        Assert.Equal(40, NativeMediaController.NativeInputSize);
    }

    [Theory]
    [InlineData("paused", "playing", "played")]
    [InlineData("stopped", "playing", "played")]
    [InlineData("playing", "paused", "paused")]
    [InlineData("playing", "stopped", "paused")]
    [InlineData("playing", "unknown", "paused")]
    [InlineData("paused", "unknown", "played")]
    [InlineData("unknown", "unknown", "toggled")]
    public void InferPlayPauseActionUsesBeforeAndAfterPlaybackState(
        string statusBefore,
        string statusAfter,
        string expectedAction)
    {
        Assert.Equal(expectedAction, NativeMediaController.InferPlayPauseAction(statusBefore, statusAfter));
    }
}
