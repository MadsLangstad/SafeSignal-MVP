using SafeSignal.Edge.PolicyService.Services;
using SafeSignal.Edge.PolicyService.Data;
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

// Add database services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AlertRepository>();
builder.Services.AddSingleton<TopologyRepository>();

// Add services to the container
builder.Services.AddSingleton<DeduplicationService>();
builder.Services.AddSingleton<AlertStateMachine>();
builder.Services.AddHostedService<MqttHandlerService>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize database
var dbService = app.Services.GetRequiredService<DatabaseService>();
await dbService.InitializeAsync();

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

// Database statistics endpoint
app.MapGet("/api/stats", async (DatabaseService db) =>
{
    var stats = await db.GetStatisticsAsync();
    return Results.Ok(stats);
});

// Recent alerts endpoint
app.MapGet("/api/alerts", async (AlertRepository alertRepo, int limit = 20) =>
{
    var alerts = await alertRepo.GetRecentAlertsAsync(limit);
    return Results.Ok(alerts);
});

// Single alert endpoint
app.MapGet("/api/alerts/{alertId}", async (string alertId, AlertRepository alertRepo) =>
{
    var alert = await alertRepo.GetAlertByIdAsync(alertId);
    return alert != null ? Results.Ok(alert) : Results.NotFound();
});

var logger = app.Logger;
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║   SafeSignal Edge - Policy Service                        ║");
logger.LogInformation("║   Version: 1.0.0-MVP (with SQLite persistence)           ║");
logger.LogInformation("║   Alert FSM | Deduplication | MQTT Handler                ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");

await app.RunAsync();
