namespace SafeSignal.Cloud.Core.Entities;

public class AuditLog
{
    public Guid Id { get; set; }

    // Who performed the action
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    // What action was performed
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public AuditCategory Category { get; set; }

    // HTTP request context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }

    // Additional details
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? AdditionalInfo { get; set; } // JSON

    // When it happened
    public DateTime Timestamp { get; set; }

    // Organization context
    public Guid? OrganizationId { get; set; }

    // Result
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public Organization? Organization { get; set; }
}

public enum AuditCategory
{
    Authentication,      // Login, logout, password changes
    Authorization,       // Permission checks, role changes
    DataAccess,         // Read operations on sensitive data
    DataModification,   // Create, update, delete
    Security,           // Security-related events
    System,             // System operations
    Alert,              // Alert-related actions
    Device              // Device operations
}
