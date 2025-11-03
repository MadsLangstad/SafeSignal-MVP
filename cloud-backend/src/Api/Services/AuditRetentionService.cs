using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Api.Services;

/// <summary>
/// Background service that periodically removes old audit logs based on retention policy
/// </summary>
public class AuditRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditRetentionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Run daily

    public AuditRetentionService(
        IServiceProvider serviceProvider,
        ILogger<AuditRetentionService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Retention Service started");

        // Wait 1 hour before first run to allow system to stabilize
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldAuditLogs(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log cleanup");
            }

            // Wait until next cleanup cycle
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Audit Retention Service stopped");
    }

    private async Task CleanupOldAuditLogs(CancellationToken cancellationToken)
    {
        // Get retention days from configuration (default: 90 days)
        var retentionDays = _configuration.GetValue<int>("AuditSettings:RetentionDays", 90);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation("Starting audit log cleanup. Retention: {RetentionDays} days, Cutoff: {CutoffDate}",
            retentionDays, cutoffDate);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SafeSignalDbContext>();

        try
        {
            // Delete audit logs older than retention period
            var deletedCount = await dbContext.AuditLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {DeletedCount} audit log entries older than {CutoffDate}",
                    deletedCount, cutoffDate);
            }
            else
            {
                _logger.LogDebug("No audit logs to clean up");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup audit logs");
            throw;
        }
    }
}
