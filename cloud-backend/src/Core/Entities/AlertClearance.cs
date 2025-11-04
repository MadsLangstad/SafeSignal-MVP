namespace SafeSignal.Cloud.Core.Entities;

/// <summary>
/// Represents a clearance action in the two-person "All Clear" workflow.
/// Each alert requires two different users to verify safety before resolution.
/// </summary>
public class AlertClearance
{
    public Guid Id { get; set; }

    /// <summary>
    /// Alert being cleared
    /// </summary>
    public Guid AlertId { get; set; }

    /// <summary>
    /// User performing the clearance
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Organization for data isolation
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Clearance step: 1 = First clearance, 2 = Second clearance
    /// </summary>
    public int ClearanceStep { get; set; }

    /// <summary>
    /// Timestamp when clearance was performed (UTC)
    /// </summary>
    public DateTime ClearedAt { get; set; }

    /// <summary>
    /// Optional notes from the person clearing the alert
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// GPS coordinates where clearance was performed (JSON format)
    /// Example: {"latitude": 40.7128, "longitude": -74.0060}
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Device/browser information from User-Agent header
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Alert Alert { get; set; } = null!;
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
