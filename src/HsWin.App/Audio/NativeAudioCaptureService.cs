using System.Diagnostics;
using System.IO;
using HsWin.Core.Audio;
using HsWin.Core.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace HsWin.App.Audio;

internal sealed class NativeAudioCaptureService : IAudioCaptureService
{
    private readonly string _recordingDirectory;
    private readonly IRuntimeLogger _logger;

    public NativeAudioCaptureService(string recordingDirectory, IRuntimeLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordingDirectory);
        _recordingDirectory = recordingDirectory;
        _logger = logger;
    }

    public IAudioRecordingSession Record(AudioRecordingOptions options, Action<AudioCaptureEvent> callback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callback);

        var device = ResolveInputDevice(options.DeviceId);
        var path = ResolveRecordingPath(options);
        var session = new NativeAudioRecordingSession(device, path, options, callback, _logger);
        session.Start();
        return session;
    }

    public IDisposable WatchLevels(AudioLevelWatchOptions options, Action<AudioCaptureEvent> callback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callback);

        var device = ResolveInputDevice(options.DeviceId);
        var watch = new NativeAudioLevelWatch(device, options, callback, _logger);
        watch.Start();
        return watch;
    }

    private static MMDevice ResolveInputDevice(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        return string.IsNullOrWhiteSpace(deviceId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
            : enumerator.GetDevice(deviceId);
    }

    private string ResolveRecordingPath(AudioRecordingOptions options)
    {
        var path = string.IsNullOrWhiteSpace(options.Path)
            ? Path.Combine(_recordingDirectory, $"recording-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.{GetDefaultExtension(options.Format)}")
            : Environment.ExpandEnvironmentVariables(options.Path);

        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Audio recording path must include a directory.", nameof(options));
        }

        Directory.CreateDirectory(directory);
        if (options.Overwrite || !File.Exists(path))
        {
            return path;
        }

        var extension = Path.GetExtension(path);
        var stem = Path.Combine(directory, Path.GetFileNameWithoutExtension(path));
        for (var suffix = 2; suffix < 10000; suffix++)
        {
            var candidate = $"{stem}-{suffix}{extension}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find an unused recording path near '{path}'.");
    }

    private static string GetDefaultExtension(AudioRecordingFormat format)
    {
        return format switch
        {
            AudioRecordingFormat.Wav => "wav",
            AudioRecordingFormat.Mp3 => "mp3",
            AudioRecordingFormat.Aac => "m4a",
            _ => "wav"
        };
    }

    private abstract class NativeAudioCaptureBase : IDisposable
    {
        private readonly Action<AudioCaptureEvent> _callback;
        private readonly int _levelIntervalMs;
        private long _sampleCount;
        private double _sumSquares;
        private double _peak;
        private long _lastLevelTick;

        protected NativeAudioCaptureBase(
            MMDevice device,
            int levelIntervalMs,
            Action<AudioCaptureEvent> callback,
            IRuntimeLogger logger)
        {
            Device = device;
            _levelIntervalMs = levelIntervalMs;
            _callback = callback;
            Logger = logger;
        }

        protected MMDevice Device { get; }

        protected WasapiCapture? Capture { get; private set; }

        protected IRuntimeLogger Logger { get; }

        protected Stopwatch Stopwatch { get; } = new();

        public virtual void Start()
        {
            Capture = new WasapiCapture(Device);
            Capture.DataAvailable += OnDataAvailable;
            Capture.RecordingStopped += OnRecordingStopped;
            Stopwatch.Start();
            _lastLevelTick = Stopwatch.ElapsedMilliseconds;
            OnCaptureReady(Capture);
            Emit(new AudioCaptureEvent(
                "started",
                Device.ID,
                Device.FriendlyName,
                SampleRate: Capture.WaveFormat.SampleRate,
                Channels: Capture.WaveFormat.Channels));
            Capture.StartRecording();
        }

        public virtual void Dispose()
        {
            StopCapture();
            Capture?.Dispose();
            Device.Dispose();
        }

        protected void StopCapture()
        {
            try
            {
                Capture?.StopRecording();
            }
            catch (InvalidOperationException)
            {
            }
        }

        protected void Emit(AudioCaptureEvent audioEvent)
        {
            try
            {
                _callback(audioEvent);
            }
            catch (Exception exception)
            {
                Logger.Error("Audio capture callback dispatch failed.", exception);
            }
        }

        protected abstract void HandleAudioData(byte[] buffer, int bytesRecorded);

        protected virtual void OnCaptureReady(WasapiCapture capture)
        {
        }

        protected virtual void HandleRecordingStopped(Exception? exception)
        {
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs eventArgs)
        {
            try
            {
                HandleAudioData(eventArgs.Buffer, eventArgs.BytesRecorded);
                MaybeEmitLevel(eventArgs.Buffer, eventArgs.BytesRecorded);
            }
            catch (Exception exception)
            {
                Logger.Error("Audio capture data handling failed.", exception);
                Emit(new AudioCaptureEvent(
                    "error",
                    Device.ID,
                    Device.FriendlyName,
                    DurationMs: Stopwatch.Elapsed.TotalMilliseconds,
                    ErrorCode: "capture_failed",
                    Message: exception.Message));
                StopCapture();
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs eventArgs)
        {
            Stopwatch.Stop();
            if (Capture is not null)
            {
                Capture.DataAvailable -= OnDataAvailable;
                Capture.RecordingStopped -= OnRecordingStopped;
            }

            HandleRecordingStopped(eventArgs.Exception);
        }

        private void MaybeEmitLevel(byte[] buffer, int bytesRecorded)
        {
            if (_levelIntervalMs <= 0 || Capture is null)
            {
                return;
            }

            var levels = AudioLevelCalculator.Calculate(buffer, bytesRecorded, Capture.WaveFormat);
            if (levels.SampleCount > 0)
            {
                _sampleCount += levels.SampleCount;
                _sumSquares += levels.SumSquares;
                _peak = Math.Max(_peak, levels.Peak);
            }

            var elapsedMs = Stopwatch.ElapsedMilliseconds;
            if (elapsedMs - _lastLevelTick < _levelIntervalMs)
            {
                return;
            }

            var rms = _sampleCount == 0 ? 0 : Math.Sqrt(_sumSquares / _sampleCount);
            Emit(new AudioCaptureEvent(
                "level",
                Device.ID,
                Device.FriendlyName,
                SampleRate: Capture.WaveFormat.SampleRate,
                Channels: Capture.WaveFormat.Channels,
                DurationMs: Stopwatch.Elapsed.TotalMilliseconds,
                Peak: Math.Round(_peak, 4),
                Rms: Math.Round(rms, 4)));

            _sampleCount = 0;
            _sumSquares = 0;
            _peak = 0;
            _lastLevelTick = elapsedMs;
        }
    }

    private sealed class NativeAudioRecordingSession : NativeAudioCaptureBase, IAudioRecordingSession
    {
        private readonly AudioRecordingOptions _options;
        private readonly object _gate = new();
        private readonly System.Threading.Timer? _stopTimer;
        private WaveFileWriter? _writer;
        private string? _workingPath;
        private int _stopped;
        private int _disposed;

        public NativeAudioRecordingSession(
            MMDevice device,
            string path,
            AudioRecordingOptions options,
            Action<AudioCaptureEvent> callback,
            IRuntimeLogger logger)
            : base(device, options.LevelIntervalMs, callback, logger)
        {
            Path = path;
            _options = options;
            if (options.MaxDurationMs is { } maxDurationMs)
            {
                _stopTimer = new System.Threading.Timer(_ => Stop(), null, maxDurationMs, Timeout.Infinite);
            }
        }

        public string Path { get; }

        public bool IsRecording => Volatile.Read(ref _stopped) == 0;

        protected override void OnCaptureReady(WasapiCapture capture)
        {
            _workingPath = _options.Format == AudioRecordingFormat.Wav
                ? Path
                : System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Path)!,
                    $".{System.IO.Path.GetFileNameWithoutExtension(Path)}-{Guid.NewGuid():N}.wav");
            _writer = new WaveFileWriter(_workingPath, capture.WaveFormat);
            Logger.Info($"Audio recording started path='{Path}' workingPath='{_workingPath}' device='{Device.FriendlyName}'.");
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            StopCapture();
        }

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Stop();
            _stopTimer?.Dispose();
            base.Dispose();
        }

        protected override void HandleAudioData(byte[] buffer, int bytesRecorded)
        {
            lock (_gate)
            {
                _writer?.Write(buffer, 0, bytesRecorded);
            }
        }

        protected override void HandleRecordingStopped(Exception? exception)
        {
            Interlocked.Exchange(ref _stopped, 1);
            _stopTimer?.Dispose();

            try
            {
                lock (_gate)
                {
                    _writer?.Dispose();
                    _writer = null;
                }

                if (exception is not null)
                {
                    Emit(new AudioCaptureEvent(
                        "error",
                        Device.ID,
                        Device.FriendlyName,
                        Path,
                        Format: FormatName(_options.Format),
                        DurationMs: Stopwatch.Elapsed.TotalMilliseconds,
                        ErrorCode: "capture_failed",
                        Message: exception.Message));
                    return;
                }

                if (_options.Format is AudioRecordingFormat.Mp3 or AudioRecordingFormat.Aac)
                {
                    EncodeRecording();
                }

                var bytes = File.Exists(Path) ? new FileInfo(Path).Length : 0;
                Logger.Info($"Audio recording stopped path='{Path}' bytes={bytes} durationMs={Stopwatch.Elapsed.TotalMilliseconds:F3}.");
                Emit(new AudioCaptureEvent(
                    "stopped",
                    Device.ID,
                    Device.FriendlyName,
                    Path,
                    FormatName(_options.Format),
                    Capture?.WaveFormat.SampleRate,
                    Capture?.WaveFormat.Channels,
                    bytes,
                    Stopwatch.Elapsed.TotalMilliseconds,
                    Reason: "stopped"));
            }
            catch (Exception encodeException)
            {
                Logger.Error("Audio recording finalization failed.", encodeException);
                Emit(new AudioCaptureEvent(
                    "error",
                    Device.ID,
                    Device.FriendlyName,
                    Path,
                    Format: FormatName(_options.Format),
                    DurationMs: Stopwatch.Elapsed.TotalMilliseconds,
                    ErrorCode: "finalize_failed",
                    Message: encodeException.Message));
            }
            finally
            {
                DeleteTemporaryWorkingFile();
            }
        }

        private void EncodeRecording()
        {
            if (string.IsNullOrWhiteSpace(_workingPath))
            {
                throw new InvalidOperationException("Audio recording has no working file.");
            }

            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            using var reader = new AudioFileReader(_workingPath);
            var bitrate = _options.BitrateKbps * 1000;
            if (_options.Format == AudioRecordingFormat.Mp3)
            {
                MediaFoundationEncoder.EncodeToMp3(reader, Path, bitrate);
            }
            else
            {
                MediaFoundationEncoder.EncodeToAac(reader, Path, bitrate);
            }
        }

        private void DeleteTemporaryWorkingFile()
        {
            if (_options.Format == AudioRecordingFormat.Wav || string.IsNullOrWhiteSpace(_workingPath))
            {
                return;
            }

            try
            {
                File.Delete(_workingPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class NativeAudioLevelWatch : NativeAudioCaptureBase
    {
        private int _disposed;

        public NativeAudioLevelWatch(
            MMDevice device,
            AudioLevelWatchOptions options,
            Action<AudioCaptureEvent> callback,
            IRuntimeLogger logger)
            : base(device, options.IntervalMs, callback, logger)
        {
        }

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            StopCapture();
            base.Dispose();
        }

        protected override void HandleAudioData(byte[] buffer, int bytesRecorded)
        {
        }

        protected override void HandleRecordingStopped(Exception? exception)
        {
            if (exception is not null)
            {
                Emit(new AudioCaptureEvent(
                    "error",
                    Device.ID,
                    Device.FriendlyName,
                    DurationMs: Stopwatch.Elapsed.TotalMilliseconds,
                    ErrorCode: "levels_failed",
                    Message: exception.Message));
            }
        }
    }

    private static string FormatName(AudioRecordingFormat format)
    {
        return format switch
        {
            AudioRecordingFormat.Wav => "wav",
            AudioRecordingFormat.Mp3 => "mp3",
            AudioRecordingFormat.Aac => "m4a",
            _ => "wav"
        };
    }

    private static class AudioLevelCalculator
    {
        public static AudioLevelStats Calculate(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
        {
            return waveFormat.Encoding switch
            {
                WaveFormatEncoding.IeeeFloat when waveFormat.BitsPerSample == 32 => CalculateFloat32(buffer, bytesRecorded),
                WaveFormatEncoding.Pcm when waveFormat.BitsPerSample == 16 => CalculatePcm16(buffer, bytesRecorded),
                WaveFormatEncoding.Pcm when waveFormat.BitsPerSample == 24 => CalculatePcm24(buffer, bytesRecorded),
                WaveFormatEncoding.Pcm when waveFormat.BitsPerSample == 32 => CalculatePcm32(buffer, bytesRecorded),
                WaveFormatEncoding.Pcm when waveFormat.BitsPerSample == 8 => CalculatePcm8(buffer, bytesRecorded),
                _ => AudioLevelStats.Empty
            };
        }

        private static AudioLevelStats CalculateFloat32(byte[] buffer, int bytesRecorded)
        {
            var count = bytesRecorded / sizeof(float);
            var stats = AudioLevelStats.Empty;
            for (var index = 0; index < count; index++)
            {
                stats = stats.AddSample(BitConverter.ToSingle(buffer, index * sizeof(float)));
            }

            return stats;
        }

        private static AudioLevelStats CalculatePcm16(byte[] buffer, int bytesRecorded)
        {
            var count = bytesRecorded / sizeof(short);
            var stats = AudioLevelStats.Empty;
            for (var index = 0; index < count; index++)
            {
                stats = stats.AddSample(BitConverter.ToInt16(buffer, index * sizeof(short)) / 32768.0);
            }

            return stats;
        }

        private static AudioLevelStats CalculatePcm24(byte[] buffer, int bytesRecorded)
        {
            var count = bytesRecorded / 3;
            var stats = AudioLevelStats.Empty;
            for (var index = 0; index < count; index++)
            {
                var offset = index * 3;
                var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
                if ((value & 0x800000) != 0)
                {
                    value |= unchecked((int)0xFF000000);
                }

                stats = stats.AddSample(value / 8388608.0);
            }

            return stats;
        }

        private static AudioLevelStats CalculatePcm32(byte[] buffer, int bytesRecorded)
        {
            var count = bytesRecorded / sizeof(int);
            var stats = AudioLevelStats.Empty;
            for (var index = 0; index < count; index++)
            {
                stats = stats.AddSample(BitConverter.ToInt32(buffer, index * sizeof(int)) / 2147483648.0);
            }

            return stats;
        }

        private static AudioLevelStats CalculatePcm8(byte[] buffer, int bytesRecorded)
        {
            var stats = AudioLevelStats.Empty;
            for (var index = 0; index < bytesRecorded; index++)
            {
                stats = stats.AddSample((buffer[index] - 128) / 128.0);
            }

            return stats;
        }
    }

    private readonly record struct AudioLevelStats(long SampleCount, double SumSquares, double Peak)
    {
        public static AudioLevelStats Empty { get; } = new(0, 0, 0);

        public AudioLevelStats AddSample(double sample)
        {
            if (double.IsNaN(sample) || double.IsInfinity(sample))
            {
                return this;
            }

            var clamped = Math.Clamp(sample, -1, 1);
            var absolute = Math.Abs(clamped);
            return new AudioLevelStats(
                SampleCount + 1,
                SumSquares + (clamped * clamped),
                Math.Max(Peak, absolute));
        }
    }
}
