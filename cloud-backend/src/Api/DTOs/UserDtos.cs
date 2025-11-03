namespace SafeSignal.Cloud.Api.DTOs;

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Phone
);

public record RegisterPushTokenRequest(
    string Token,
    string Platform  // "ios" or "android"
);
