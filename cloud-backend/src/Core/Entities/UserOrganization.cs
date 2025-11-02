namespace SafeSignal.Cloud.Core.Entities;

public class UserOrganization
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}

public enum UserRole
{
    SuperAdmin,
    OrgAdmin,
    Manager,
    Operator,
    Viewer
}
