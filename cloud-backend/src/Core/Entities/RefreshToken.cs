using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SafeSignal.Cloud.Core.Entities;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(100)]
    public string? RevokedByIp { get; set; }

    [MaxLength(255)]
    public string? ReplacedByToken { get; set; }

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsRevoked => RevokedAt != null;

    [NotMapped]
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation property
    public User? User { get; set; }
}
