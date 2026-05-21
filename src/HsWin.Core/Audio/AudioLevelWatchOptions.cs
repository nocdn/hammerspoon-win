namespace HsWin.Core.Audio;

public sealed record AudioLevelWatchOptions(
    string? DeviceId,
    int IntervalMs)
{
    public const int DefaultIntervalMs = 100;
    public const int MinimumIntervalMs = 25;
    public const int MaximumIntervalMs = 5000;

    public static AudioLevelWatchOptions Default { get; } = new(null, DefaultIntervalMs);
}
