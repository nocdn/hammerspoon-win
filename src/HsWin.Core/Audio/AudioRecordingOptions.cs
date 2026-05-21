namespace HsWin.Core.Audio;

public sealed record AudioRecordingOptions(
    string? DeviceId,
    string? Path,
    AudioRecordingFormat Format,
    bool Overwrite,
    int BitrateKbps,
    int LevelIntervalMs,
    int? MaxDurationMs)
{
    public const int DefaultBitrateKbps = 192;
    public const int DefaultLevelIntervalMs = 250;
    public const int MinimumLevelIntervalMs = 25;
    public const int MaximumLevelIntervalMs = 5000;
    public const int MinimumBitrateKbps = 32;
    public const int MaximumBitrateKbps = 320;

    public static AudioRecordingOptions Default { get; } =
        new(null, null, AudioRecordingFormat.Wav, Overwrite: false, DefaultBitrateKbps, DefaultLevelIntervalMs, null);
}
