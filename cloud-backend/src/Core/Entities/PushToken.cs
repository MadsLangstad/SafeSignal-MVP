namespace SafeSignal.Cloud.Core.Entities;

public class PushToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // "ios" or "android"
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt == null;

    // Navigation properties
    public User? User { get; set; }
}
