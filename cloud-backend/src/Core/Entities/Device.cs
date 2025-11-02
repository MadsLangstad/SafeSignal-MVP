namespace SafeSignal.Cloud.Core.Entities;

public class Device
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid? RoomId { get; set; }
    public DeviceType DeviceType { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? HardwareVersion { get; set; }
    public string? SerialNumber { get; set; }
    public string? MacAddress { get; set; }
    public DateTime? ProvisionedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DeviceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Metadata { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public Room? Room { get; set; }
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<DeviceMetric> DeviceMetrics { get; set; } = new List<DeviceMetric>();
}

public enum DeviceType
{
    Button,
    Gateway,
    Sensor
}

public enum DeviceStatus
{
    Active,
    Inactive,
    Maintenance,
    Offline
}
