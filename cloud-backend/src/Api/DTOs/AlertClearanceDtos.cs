namespace SafeSignal.Cloud.Api.DTOs;

// Request DTOs
public record ClearAlertRequest(
    string? Notes,
    LocationDto? Location
);

public record LocationDto(
    double Latitude,
    double Longitude
);

// Response DTOs
public record ClearAlertResponse(
    Guid AlertId,
    string Status,
    string Message,
    int ClearanceStep,
    Guid ClearanceId,
    string ClearedBy,
    DateTime ClearedAt,
    bool RequiresSecondClearance,
    ClearanceInfoDto? FirstClearance = null,
    ClearanceInfoDto? SecondClearance = null
);

public record ClearanceInfoDto(
    Guid UserId,
    string UserName,
    DateTime ClearedAt
);

public record AlertClearanceHistoryResponse(
    Guid AlertId,
    string Status,
    List<AlertClearanceDto> Clearances
);

public record AlertClearanceDto(
    Guid Id,
    int ClearanceStep,
    Guid UserId,
    string UserName,
    string UserEmail,
    DateTime ClearedAt,
    string? Notes,
    LocationDto? Location,
    string? DeviceInfo
);
