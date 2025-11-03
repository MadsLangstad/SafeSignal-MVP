using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Api.DTOs;

public record CreateAlertRequest(
    string AlertId,
    Guid? DeviceId,
    Guid? RoomId,
    AlertSeverity Severity,
    string AlertType,
    AlertSource Source,
    string? Metadata
);

public record UpdateAlertRequest(
    AlertStatus Status,
    DateTime? ResolvedAt
);

public record TriggerAlertRequest(
    Guid BuildingId,
    Guid? DeviceId,
    Guid? RoomId,
    string? Mode,
    string? Metadata
);

public record AlertResponse(
    Guid Id,
    string AlertId,
    Guid OrganizationId,
    Guid? BuildingId,
    Guid? DeviceId,
    Guid? RoomId,
    DateTime TriggeredAt,
    DateTime? ResolvedAt,
    AlertSeverity Severity,
    string AlertType,
    AlertStatus Status,
    AlertSource Source,
    string? Metadata,
    DateTime CreatedAt
);
