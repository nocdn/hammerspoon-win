namespace HsWin.Core.Audio;

public sealed record AudioDeviceSnapshot(
    string Id,
    string Name,
    bool IsDefault,
    double Volume,
    bool Muted);
