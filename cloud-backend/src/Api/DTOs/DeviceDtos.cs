using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Api.DTOs;

public record RegisterDeviceRequest(
    string DeviceId,
    Guid OrganizationId,
    string? SerialNumber,
    string? MacAddress,
    string? HardwareVersion,
    string? Metadata
);

public record UpdateDeviceRequest(
    Guid? RoomId,
    string? FirmwareVersion,
    DeviceStatus? Status,
    string? Metadata
);

public record DeviceResponse(
    Guid Id,
    string DeviceId,
    Guid OrganizationId,
    Guid? RoomId,
    DeviceType DeviceType,
    string? FirmwareVersion,
    string? HardwareVersion,
    string? SerialNumber,
    string? MacAddress,
    DateTime? ProvisionedAt,
    DateTime? LastSeenAt,
    DeviceStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Metadata
);
