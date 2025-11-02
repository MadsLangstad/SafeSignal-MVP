namespace SafeSignal.Cloud.Core.Entities;

public class Room
{
    public Guid Id { get; set; }
    public Guid FloorId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int? Capacity { get; set; }
    public string? RoomType { get; set; }

    // Navigation properties
    public Floor Floor { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
