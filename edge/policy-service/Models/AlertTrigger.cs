using System.Text.Json.Serialization;

namespace SafeSignal.Edge.PolicyService.Models;

/// <summary>
/// Alert trigger message received from ESP32 devices or mobile apps
/// </summary>
public class AlertTrigger
{
    [JsonPropertyName("alertId")]
    public required string AlertId { get; init; }

    [JsonPropertyName("tenantId")]
    public required string TenantId { get; init; }

    [JsonPropertyName("buildingId")]
    public required string BuildingId { get; init; }

    [JsonPropertyName("sourceDeviceId")]
    public required string SourceDeviceId { get; init; }

    [JsonPropertyName("sourceRoomId")]
    public required string SourceRoomId { get; init; }

    [JsonPropertyName("origin")]
    public required string Origin { get; init; } // ESP32, APP, EDGE

    [JsonPropertyName("causalChainId")]
    public required string CausalChainId { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; } // SILENT, AUDIBLE

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; init; }
}

/// <summary>
/// Internal alert event after validation and enrichment
/// </summary>
public class AlertEvent
{
    public required string AlertId { get; init; }
    public required string TenantId { get; init; }
    public required string BuildingId { get; init; }
    public required string SourceRoomId { get; init; }
    public required string CausalChainId { get; init; }
    public required string Mode { get; init; }
    public required AlertState State { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
    public List<string> TargetRooms { get; init; } = new();
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public enum AlertState
{
    Triggered,
    Validated,
    PolicyEvaluated,
    FanOutInitiated,
    Cleared,
    Rejected
}
