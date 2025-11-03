using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log an authentication event (login, logout, password change)
    /// </summary>
    Task LogAuthenticationAsync(
        string action,
        Guid? userId,
        string? userEmail,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a data modification event (create, update, delete)
    /// </summary>
    Task LogDataModificationAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a security-related event
    /// </summary>
    Task LogSecurityEventAsync(
        string action,
        Guid? userId,
        string? userEmail,
        Guid? organizationId,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Log an alert-related event
    /// </summary>
    Task LogAlertEventAsync(
        string action,
        Guid? alertId,
        Guid? deviceId,
        Guid? organizationId,
        Guid? userId,
        string? userEmail,
        string? ipAddress = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query audit logs with filtering
    /// </summary>
    Task<List<AuditLog>> QueryAsync(
        Guid? userId = null,
        Guid? organizationId = null,
        AuditCategory? category = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? limit = 100,
        CancellationToken cancellationToken = default);
}
