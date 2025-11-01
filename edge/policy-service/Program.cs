using SafeSignal.Edge.PolicyService.Services;
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

// Add services to the container
builder.Services.AddSingleton<DeduplicationService>();
builder.Services.AddSingleton<AlertStateMachine>();
builder.Services.AddHostedService<MqttHandlerService>();

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
    service = "SafeSignal Policy Service",
    version = "1.0.0-mvp",
    status = "running",
    timestamp = DateTimeOffset.UtcNow
});

var logger = app.Logger;
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║   SafeSignal Edge - Policy Service                        ║");
logger.LogInformation("║   Version: 1.0.0-MVP                                      ║");
logger.LogInformation("║   Alert FSM | Deduplication | MQTT Handler                ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");

await app.RunAsync();
