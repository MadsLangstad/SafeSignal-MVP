using System.Text.Json.Serialization;

namespace SafeSignal.Edge.PolicyService.Models;

/// <summary>
/// PA (Public Address) playback command sent to PA service
/// </summary>
public class PaCommand
{
    [JsonPropertyName("alertId")]
    public required string AlertId { get; init; }

    [JsonPropertyName("roomId")]
    public required string RoomId { get; init; }

    [JsonPropertyName("clipRef")]
    public required string ClipRef { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("causalChainId")]
    public required string CausalChainId { get; init; }
}

/// <summary>
/// PA playback status acknowledgement from PA service
/// </summary>
public class PaStatus
{
    [JsonPropertyName("alertId")]
    public required string AlertId { get; init; }

    [JsonPropertyName("roomId")]
    public required string RoomId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; } // OK, ERROR, PLAYING, COMPLETED

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
