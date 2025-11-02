namespace SafeSignal.Cloud.Core.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public string AlertId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? RoomId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public AlertSeverity Severity { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public AlertStatus Status { get; set; }
    public AlertSource Source { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public Device? Device { get; set; }
    public Room? Room { get; set; }
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AlertStatus
{
    New,
    Acknowledged,
    Resolved,
    Cancelled
}

public enum AlertSource
{
    Button,
    Mobile,
    Web,
    Api
}
