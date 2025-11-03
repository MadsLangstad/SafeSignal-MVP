using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Api.Services;

public class AuditService : IAuditService
{
    private readonly SafeSignalDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(SafeSignalDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        try
        {
            // Set timestamp if not already set
            if (auditLog.Timestamp == default)
            {
                auditLog.Timestamp = DateTime.UtcNow;
            }

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Never fail the original operation due to audit logging failure
            _logger.LogError(ex, "Failed to write audit log for action {Action}", auditLog.Action);
        }
    }

    public async Task LogAuthenticationAsync(
        string action,
        Guid? userId,
        string? userEmail,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = "User",
            EntityId = userId,
            Category = AuditCategory.Authentication,
            UserId = userId,
            UserEmail = userEmail,
            Success = success,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(auditLog, cancellationToken);
    }

    public async Task LogDataModificationAsync(
        string action,
        string entityType,
        Guid? entityId,
        Guid? userId,
        string? userEmail,
        Guid? organizationId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Category = AuditCategory.DataModification,
            UserId = userId,
            UserEmail = userEmail,
            OrganizationId = organizationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            Success = true,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(auditLog, cancellationToken);
    }

    public async Task LogSecurityEventAsync(
        string action,
        Guid? userId,
        string? userEmail,
        Guid? organizationId,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = "Security",
            Category = AuditCategory.Security,
            UserId = userId,
            UserEmail = userEmail,
            OrganizationId = organizationId,
            Success = success,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            AdditionalInfo = additionalInfo,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(auditLog, cancellationToken);
    }

    public async Task LogAlertEventAsync(
        string action,
        Guid? alertId,
        Guid? deviceId,
        Guid? organizationId,
        Guid? userId,
        string? userEmail,
        string? ipAddress = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = "Alert",
            EntityId = alertId,
            Category = AuditCategory.Alert,
            UserId = userId,
            UserEmail = userEmail,
            OrganizationId = organizationId,
            IpAddress = ipAddress,
            AdditionalInfo = additionalInfo,
            Success = true,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(auditLog, cancellationToken);
    }

    public async Task<List<AuditLog>> QueryAsync(
        Guid? userId = null,
        Guid? organizationId = null,
        AuditCategory? category = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(a => a.OrganizationId == organizationId.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(a => a.Category == category.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate.Value);
        }

        query = query.OrderByDescending(a => a.Timestamp);

        if (limit.HasValue && limit.Value > 0)
        {
            query = query.Take(Math.Min(limit.Value, 1000)); // Max 1000 records
        }

        return await query.ToListAsync(cancellationToken);
    }
}
