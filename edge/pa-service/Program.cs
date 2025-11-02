using SafeSignal.Edge.PaService.Services;
using SafeSignal.Edge.PaService.Configuration;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
});

// Configure MinIO settings
var minioSettings = new MinioSettings();
builder.Configuration.GetSection("Minio").Bind(minioSettings);
builder.Services.AddSingleton(minioSettings);

// Add services
builder.Services.AddSingleton<AudioStorageService>();
builder.Services.AddSingleton<AudioPlaybackService>();
builder.Services.AddHostedService<MqttSubscriberService>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure HTTP request pipeline
app.UseRouting();

// Prometheus metrics endpoint
app.UseMetricServer(); // Exposes /metrics
app.UseHttpMetrics();

// Health check endpoint
app.MapHealthChecks("/health");

// Basic info endpoint
app.MapGet("/", () => new
{
    service = "SafeSignal PA Service",
    version = "1.0.0-mvp",
    status = "running",
    timestamp = DateTimeOffset.UtcNow
});

// Audio clip management endpoints
app.MapGet("/api/clips", async (AudioPlaybackService audioPlayback) =>
{
    var clips = await audioPlayback.ListAvailableClipsAsync();
    return Results.Ok(new { clips, count = clips.Count });
});

app.MapGet("/api/clips/{clipRef}", async (string clipRef, AudioPlaybackService audioPlayback) =>
{
    var exists = await audioPlayback.ClipExistsAsync(clipRef);
    if (!exists)
    {
        return Results.NotFound(new { error = "Audio clip not found", clipRef });
    }

    var audioData = await audioPlayback.GetAudioClipAsync(clipRef);
    if (audioData == null)
    {
        return Results.Problem("Failed to load audio clip");
    }

    return Results.File(audioData, "audio/wav", $"{clipRef}.wav");
});

app.MapPost("/api/clips/{clipRef}", async (string clipRef, IFormFile file, AudioStorageService audioStorage, ILogger<Program> logger) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "No file provided" });
    }

    if (!file.ContentType.StartsWith("audio/"))
    {
        return Results.BadRequest(new { error = "File must be an audio file" });
    }

    try
    {
        using var stream = file.OpenReadStream();
        var success = await audioStorage.UploadAudioClipAsync($"{clipRef}.wav", stream, file.ContentType);

        if (success)
        {
            logger.LogInformation("Audio clip uploaded successfully: {ClipRef}", clipRef);
            return Results.Ok(new { message = "Audio clip uploaded successfully", clipRef });
        }
        else
        {
            return Results.Problem("Failed to upload audio clip");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error uploading audio clip: {ClipRef}", clipRef);
        return Results.Problem($"Error uploading audio clip: {ex.Message}");
    }
}).DisableAntiforgery();

var logger = app.Logger;
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║   SafeSignal Edge - PA Service                            ║");
logger.LogInformation("║   Version: 1.0.0-MVP                                      ║");
logger.LogInformation("║   MinIO Storage | Audio Playback | MQTT Subscriber        ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");

await app.RunAsync();
