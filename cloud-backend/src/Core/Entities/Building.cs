namespace SafeSignal.Cloud.Core.Entities;

public class Building
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int FloorCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Site Site { get; set; } = null!;
    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}
