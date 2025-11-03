using Microsoft.IdentityModel.Tokens;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SafeSignal.Cloud.Api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly SafeSignalDbContext _context;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IConfiguration configuration,
        SafeSignalDbContext context,
        ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public string GenerateAccessToken(Guid userId, string email, Guid organizationId, List<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        // Prioritize environment variable, fall back to configuration (same as Program.cs)
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        var expiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "1440"); // Default 24 hours

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new("organizationId", organizationId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add role claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public Task<Guid?> ValidateAccessTokenAsync(string token)
    {
        return ValidateAccessTokenAsync(token, validateLifetime: true);
    }

    public Task<Guid?> ValidateAccessTokenAsync(string token, bool validateLifetime)
    {
        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            // Prioritize environment variable, fall back to configuration (same as Program.cs)
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? jwtSettings["SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = !string.IsNullOrEmpty(issuer),
                ValidIssuer = issuer,
                ValidateAudience = !string.IsNullOrEmpty(audience),
                ValidAudience = audience,
                ValidateLifetime = validateLifetime,
                ClockSkew = validateLifetime ? TimeSpan.Zero : TimeSpan.FromMinutes(5) // Allow 5min clock skew for refresh
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Task.FromResult<Guid?>(userId);
            }

            return Task.FromResult<Guid?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Task.FromResult<Guid?>(null);
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId)
    {
        var storedToken = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

        if (storedToken == null || !storedToken.IsActive)
        {
            _logger.LogWarning("Invalid or inactive refresh token for user {UserId}", userId);
            return false;
        }

        return true;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Refresh token revoked for user {UserId}", token.UserId);
        }
    }

    public async Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresAt)
    {
        // Remove old refresh tokens for this user (keep only the latest)
        var oldTokens = await _context.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        _context.Set<RefreshToken>().RemoveRange(oldTokens);

        // Save new refresh token
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = refreshToken,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<RefreshToken>().Add(newToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token saved for user {UserId}, expires at {ExpiresAt}", userId, expiresAt);
    }
}
