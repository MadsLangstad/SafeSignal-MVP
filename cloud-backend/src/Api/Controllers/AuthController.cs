using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
            return Unauthorized(new { error = "Account is not active" });
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        // Get user's primary organization
        var primaryOrganization = user.UserOrganizations.FirstOrDefault();
        if (primaryOrganization == null)
        {
            _logger.LogWarning("User {Email} has no organization assigned", user.Email);
            return Unauthorized(new { error = "User has no organization assigned" });
        }

        // Generate JWT access token with organizationId and roles
        var roles = user.UserOrganizations
            .Select(uo => uo.Role.ToString())
            .Distinct()
            .ToList();

        _logger.LogInformation("User {Email} has {RoleCount} roles: {Roles}",
            user.Email, roles.Count, string.Join(", ", roles));

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, primaryOrganization.OrganizationId, roles);

        // Generate and save refresh token
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        await _jwtTokenService.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

        // Calculate access token expiry
        var accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "1440");
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes);

        _logger.LogInformation("User logged in: {Email}", user.Email);

        var tokens = new AuthTokensResponse(accessToken, refreshToken, accessTokenExpiresAt);
        var userResponse = MapToUserResponse(user);
        var response = new LoginResponse(userResponse, tokens);

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokensResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Validate access token without checking lifetime (allow expired tokens for refresh)
        var userId = await _jwtTokenService.ValidateAccessTokenAsync(request.AccessToken, validateLifetime: false);
        if (userId == null)
        {
            _logger.LogWarning("Invalid access token in refresh request");
            return Unauthorized(new { error = "Invalid token" });
        }

        var isValidRefreshToken = await _jwtTokenService.ValidateRefreshTokenAsync(request.RefreshToken, userId.Value);
        if (!isValidRefreshToken)
        {
            _logger.LogWarning("Invalid refresh token for user {UserId}", userId);
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null || user.Status != UserStatus.Active)
        {
            return Unauthorized(new { error = "User not found or inactive" });
        }

        // Get user's primary organization
        var primaryOrganization = user.UserOrganizations.FirstOrDefault();
        if (primaryOrganization == null)
        {
            _logger.LogWarning("User {UserId} has no organization assigned", userId);
            return Unauthorized(new { error = "User has no organization assigned" });
        }

        // Generate new access token with organizationId and roles
        var roles = user.UserOrganizations
            .Select(uo => uo.Role.ToString())
            .Distinct()
            .ToList();

        _logger.LogInformation("Refresh: User {UserId} has {RoleCount} roles: {Roles}",
            user.Id, roles.Count, string.Join(", ", roles));

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, primaryOrganization.OrganizationId, roles);

        // Generate new refresh token and revoke old one
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        await _jwtTokenService.SaveRefreshTokenAsync(user.Id, newRefreshToken, refreshTokenExpiresAt);

        // Calculate access token expiry
        var accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "1440");
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes);

        _logger.LogInformation("Tokens refreshed for user {UserId}", user.Id);

        var tokens = new AuthTokensResponse(newAccessToken, newRefreshToken, accessTokenExpiresAt);
        return Ok(tokens);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        _logger.LogInformation("User logged out");
        return Ok(new { message = "Logged out successfully" });
    }

    private static UserResponse MapToUserResponse(User user)
    {
        var primaryOrg = user.UserOrganizations.FirstOrDefault();
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(fullName))
        {
            fullName = user.Email.Split('@')[0];
        }

        return new UserResponse(
            user.Id.ToString(),
            user.Email,
            fullName,
            primaryOrg?.OrganizationId.ToString() ?? Guid.Empty.ToString(),
            null, // AssignedBuildingId
            null, // AssignedRoomId
            user.Phone,
            user.CreatedAt
        );
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }
}
