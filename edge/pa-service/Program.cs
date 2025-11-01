using SafeSignal.Edge.PaService.Services;
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

// Add services
builder.Services.AddSingleton<TtsStubService>();
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

var logger = app.Logger;
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║   SafeSignal Edge - PA Service                            ║");
logger.LogInformation("║   Version: 1.0.0-MVP                                      ║");
logger.LogInformation("║   TTS Stub | Audio Playback | MQTT Subscriber             ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");

await app.RunAsync();
