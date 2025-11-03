namespace SafeSignal.Cloud.Api.DTOs;

public record BuildingResponse(
    string Id,
    string TenantId,
    string Name,
    string Address,
    List<RoomResponse> Rooms
);

public record CreateBuildingRequest(
    Guid SiteId,
    string Name,
    string? Address,
    int FloorCount
);
