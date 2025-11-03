namespace SafeSignal.Cloud.Api.DTOs;

public record RoomResponse(
    string Id,
    string BuildingId,
    string Name,
    int? Capacity,
    string? Floor
);

public record CreateRoomRequest(
    Guid FloorId,
    string RoomNumber,
    string? Name,
    string? RoomType,
    int? Capacity
);

public record UpdateRoomRequest(
    string? RoomNumber,
    string? Name,
    string? RoomType,
    int? Capacity
);
