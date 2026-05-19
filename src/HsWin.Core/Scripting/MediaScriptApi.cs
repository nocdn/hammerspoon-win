using HsWin.Core.Logging;
using HsWin.Core.Media;
using System.Diagnostics;

namespace HsWin.Core.Scripting;

public sealed class MediaScriptApi
{
    private readonly IMediaController _media;
    private readonly IRuntimeLogger _logger;

    public MediaScriptApi(IMediaController media, IRuntimeLogger logger)
    {
        _media = media;
        _logger = logger;
    }

    public string PlayPauseJson()
    {
        var startedAt = Stopwatch.GetTimestamp();
        _logger.Info("Script hs.media.playPause() requested.");
        var result = _media.PlayPause();
        _logger.Info($"Script hs.media.playPause() completed action='{result.Action}' statusBefore='{result.StatusBefore}' statusAfter='{result.StatusAfter}' success={result.Success} backend='{result.Backend}' elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
        return ScriptJson.Serialize(result);
    }

    public string PreviousTrackJson()
    {
        _logger.Info("Script hs.media.previousTrack() requested.");
        var result = _media.PreviousTrack();
        _logger.Info($"Script hs.media.previousTrack() completed success={result.Success} backend='{result.Backend}'.");
        return ScriptJson.Serialize(result);
    }

    public string NextTrackJson()
    {
        _logger.Info("Script hs.media.nextTrack() requested.");
        var result = _media.NextTrack();
        _logger.Info($"Script hs.media.nextTrack() completed success={result.Success} backend='{result.Backend}'.");
        return ScriptJson.Serialize(result);
    }
}
