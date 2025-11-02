namespace SafeSignal.Cloud.Core.Entities;

public class DeviceMetric
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public int? Rssi { get; set; }
    public decimal? BatteryVoltage { get; set; }
    public long? UptimeSeconds { get; set; }
    public long? FreeHeapBytes { get; set; }
    public int AlertCount { get; set; }
    public string? Metadata { get; set; }

    // Navigation properties
    public Device Device { get; set; } = null!;
}
