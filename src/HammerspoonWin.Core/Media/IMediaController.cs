namespace HammerspoonWin.Core.Media;

public interface IMediaController
{
    MediaCommandResult PlayPause();

    MediaCommandResult PreviousTrack();

    MediaCommandResult NextTrack();
}
