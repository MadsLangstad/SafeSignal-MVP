namespace SafeSignal.Cloud.Core.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public UserRole Role { get; set; }
    public string Resource { get; set; } = string.Empty;
    public PermissionAction Action { get; set; }
}

public enum PermissionAction
{
    Create,
    Read,
    Update,
    Delete
}
