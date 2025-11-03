namespace SafeSignal.Cloud.Api.DTOs;

public record LoginRequest(
    string Email,
    string Password
);

public record LoginResponse(
    UserResponse User,
    AuthTokensResponse Tokens
);

public record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record UserResponse(
    string Id,
    string Email,
    string Name,
    string TenantId,
    string? AssignedBuildingId,
    string? AssignedRoomId,
    string? PhoneNumber,
    DateTime CreatedAt
);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);

public record LogoutRequest(
    string RefreshToken
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
