using Prometheus;

namespace SafeSignal.Edge.PaService.Services;

/// <summary>
/// Text-to-Speech stub service
/// Simulates TTS generation and audio playback with configurable delay
/// In production, this would integrate with real TTS engine (espeak-ng, Azure TTS, etc.)
/// </summary>
public class TtsStubService
{
    private readonly ILogger<TtsStubService> _logger;
    private readonly Random _random = new();

    // Prometheus metrics
    private static readonly Histogram TtsGenerationDuration = Metrics.CreateHistogram(
        "tts_generation_duration_seconds",
        "Time to generate TTS audio",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 8) // 50ms to ~6s
        });

    private static readonly Histogram PlaybackDuration = Metrics.CreateHistogram(
        "pa_playback_duration_seconds",
        "Time to play audio on PA system",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(1, 1, 10) // 1s to 10s
        });

    public TtsStubService(ILogger<TtsStubService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate TTS audio from text (simulated)
    /// </summary>
    public async Task<byte[]> GenerateTtsAsync(string text, CancellationToken cancellationToken = default)
    {
        using var _ = TtsGenerationDuration.NewTimer();

        _logger.LogInformation("Generating TTS: Text='{Text}'", text);

        // Simulate TTS generation delay (50-200ms)
        var delay = _random.Next(50, 200);
        await Task.Delay(delay, cancellationToken);

        // Return fake audio data
        var audioData = new byte[1024];
        _random.NextBytes(audioData);

        _logger.LogDebug("TTS generated: Size={Size} bytes, Duration={Duration}ms",
            audioData.Length, delay);

        return audioData;
    }

    /// <summary>
    /// Play audio on PA system (simulated)
    /// </summary>
    public async Task<PlaybackResult> PlayAudioAsync(string roomId, byte[] audioData, CancellationToken cancellationToken = default)
    {
        using var _ = PlaybackDuration.NewTimer();

        _logger.LogInformation("Playing audio: Room={RoomId}, Size={Size} bytes",
            roomId, audioData.Length);

        // Simulate playback duration (2-4 seconds for emergency message)
        var playbackMs = _random.Next(2000, 4000);
        await Task.Delay(playbackMs, cancellationToken);

        // Simulate occasional failures (1% failure rate)
        var success = _random.Next(100) > 1;

        var result = new PlaybackResult
        {
            Success = success,
            RoomId = roomId,
            DurationMs = playbackMs,
            ErrorMessage = success ? null : "Simulated PA system error"
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
        }

        return result;
    }

    /// <summary>
    /// Get pre-recorded audio clip (simulated)
    /// </summary>
    public async Task<byte[]> GetAudioClipAsync(string clipRef, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading audio clip: ClipRef={ClipRef}", clipRef);

        // Simulate clip loading delay
        await Task.Delay(_random.Next(20, 100), cancellationToken);

        // Return fake audio data
        var audioData = new byte[2048];
        _random.NextBytes(audioData);

        return audioData;
    }
}

public class PlaybackResult
{
    public required bool Success { get; init; }
    public required string RoomId { get; init; }
    public required int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
