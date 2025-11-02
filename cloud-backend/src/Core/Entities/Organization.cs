namespace SafeSignal.Cloud.Core.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public OrganizationStatus Status { get; set; }
    public string? Metadata { get; set; }

    // Navigation properties
    public ICollection<Site> Sites { get; set; } = new List<Site>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
}

public enum OrganizationStatus
{
    Active,
    Suspended,
    Deleted
}
