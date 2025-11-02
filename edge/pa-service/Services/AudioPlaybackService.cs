using Prometheus;

namespace SafeSignal.Edge.PaService.Services;

/// <summary>
/// Real audio playback service using MinIO-stored audio clips
/// Replaces TtsStubService with actual audio file retrieval
/// </summary>
public class AudioPlaybackService
{
    private readonly AudioStorageService _audioStorage;
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly Random _random = new();

    // Prometheus metrics
    private static readonly Histogram AudioLoadDuration = Metrics.CreateHistogram(
        "audio_load_duration_seconds",
        "Time to load audio from storage",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) // 10ms to ~10s
        });

    private static readonly Histogram PlaybackDuration = Metrics.CreateHistogram(
        "pa_playback_duration_seconds",
        "Time to play audio on PA system",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(1, 1, 10) // 1s to 10s
        });

    private static readonly Counter AudioLoadErrors = Metrics.CreateCounter(
        "audio_load_errors_total",
        "Total number of audio load errors");

    private static readonly Counter PlaybackErrors = Metrics.CreateCounter(
        "playback_errors_total",
        "Total number of playback errors");

    public AudioPlaybackService(
        AudioStorageService audioStorage,
        ILogger<AudioPlaybackService> logger)
    {
        _audioStorage = audioStorage;
        _logger = logger;
    }

    /// <summary>
    /// Get audio clip from MinIO storage
    /// </summary>
    public async Task<byte[]?> GetAudioClipAsync(string clipRef, CancellationToken cancellationToken = default)
    {
        using var _ = AudioLoadDuration.NewTimer();

        try
        {
            _logger.LogInformation("Loading audio clip: ClipRef={ClipRef}", clipRef);

            var audioData = await _audioStorage.GetAudioClipAsync(clipRef, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("Audio clip not found or empty: {ClipRef}", clipRef);
                AudioLoadErrors.Inc();
                return null;
            }

            _logger.LogInformation("Audio clip loaded successfully: ClipRef={ClipRef}, Size={Size} bytes",
                clipRef, audioData.Length);

            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load audio clip: {ClipRef}", clipRef);
            AudioLoadErrors.Inc();
            return null;
        }
    }

    /// <summary>
    /// Play audio on PA system
    /// In a real implementation, this would interface with hardware audio output
    /// For now, we simulate playback with actual audio data from MinIO
    /// </summary>
    public async Task<PlaybackResult> PlayAudioAsync(
        string roomId,
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        using var _ = PlaybackDuration.NewTimer();

        try
        {
            _logger.LogInformation("Playing audio: Room={RoomId}, Size={Size} bytes",
                roomId, audioData.Length);

            // Calculate estimated playback duration based on audio data size
            // Assuming 44.1kHz, 16-bit, mono WAV: ~88.2 KB per second
            // For actual implementation, parse WAV header to get duration
            var estimatedDurationMs = (int)(audioData.Length / 88.2);

            // Clamp duration to reasonable range for emergency messages (1-10 seconds)
            var playbackMs = Math.Max(1000, Math.Min(estimatedDurationMs, 10000));

            // TODO: Replace with actual audio hardware playback
            // For now, simulate playback delay
            await Task.Delay(playbackMs, cancellationToken);

            // Simulate occasional failures (0.5% failure rate for real system)
            var success = _random.Next(1000) > 5;

            var result = new PlaybackResult
            {
                Success = success,
                RoomId = roomId,
                DurationMs = playbackMs,
                ErrorMessage = success ? null : "PA hardware communication error"
            };

            if (success)
            {
                _logger.LogInformation("Audio playback completed: Room={RoomId}, Duration={Duration}ms",
                    roomId, playbackMs);
            }
            else
            {
                _logger.LogError("Audio playback failed: Room={RoomId}, Error={Error}",
                    roomId, result.ErrorMessage);
                PlaybackErrors.Inc();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audio playback: Room={RoomId}", roomId);
            PlaybackErrors.Inc();

            return new PlaybackResult
            {
                Success = false,
                RoomId = roomId,
                DurationMs = 0,
                ErrorMessage = $"Playback exception: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// List all available audio clips
    /// </summary>
    public async Task<List<string>> ListAvailableClipsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clips = await _audioStorage.ListAvailableClipsAsync(cancellationToken);
            _logger.LogInformation("Available audio clips: {Count}", clips.Count);
            return clips;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list available audio clips");
            return new List<string>();
        }
    }

    /// <summary>
    /// Check if a clip exists in storage
    /// </summary>
    public async Task<bool> ClipExistsAsync(string clipRef, CancellationToken cancellationToken = default)
    {
        return await _audioStorage.ClipExistsAsync(clipRef, cancellationToken);
    }
}

public class PlaybackResult
{
    public required bool Success { get; init; }
    public required string RoomId { get; init; }
    public required int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
