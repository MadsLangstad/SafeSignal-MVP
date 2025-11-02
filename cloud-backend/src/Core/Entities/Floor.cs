namespace SafeSignal.Cloud.Core.Entities;

public class Floor
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }
    public int FloorNumber { get; set; }
    public string? Name { get; set; }

    // Navigation properties
    public Building Building { get; set; } = null!;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
