namespace HsWin.Core.Media;

public sealed class NullMediaController : IMediaController
{
    public static NullMediaController Instance { get; } = new();

    private NullMediaController()
    {
    }

    public MediaCommandResult PlayPause()
    {
        throw new NotSupportedException("Media controls are not available in this runtime.");
    }

    public MediaCommandResult PreviousTrack()
    {
        throw new NotSupportedException("Media controls are not available in this runtime.");
    }

    public MediaCommandResult NextTrack()
    {
        throw new NotSupportedException("Media controls are not available in this runtime.");
    }
}
