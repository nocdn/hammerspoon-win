namespace HsWin.Core.Audio;

public sealed record AudioDeviceVolumeSnapshot(
    string Id,
    string Name,
    double Volume,
    bool Muted);
