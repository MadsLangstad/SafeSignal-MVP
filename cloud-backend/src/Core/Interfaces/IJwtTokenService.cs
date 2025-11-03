namespace SafeSignal.Cloud.Core.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, List<string> roles);
    string GenerateRefreshToken();
    Task<Guid?> ValidateAccessTokenAsync(string token);
    Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresAt);
}
