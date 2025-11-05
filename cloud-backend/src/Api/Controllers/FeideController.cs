using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/auth/feide")]
public class FeideController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly ILogger<FeideController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public FeideController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IAuditService auditService,
        ILogger<FeideController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpPost("callback")]
    public async Task<ActionResult<LoginResponse>> Callback([FromBody] FeideCallbackRequest request)
    {
        try
        {
            var clientId = _configuration["Feide:ClientId"];
            var clientSecret = _configuration["Feide:ClientSecret"];
            var tokenEndpoint = _configuration["Feide:TokenEndpoint"] ?? "https://auth.dataporten.no/oauth/token";

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("Feide configuration missing");
                return BadRequest(new { error = "Feide authentication not configured" });
            }

            // Exchange authorization code for access token
            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", request.Code },
                { "redirect_uri", request.RedirectUri },
                { "code_verifier", request.CodeVerifier },
                { "client_id", clientId }
            };

            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authHeader}");

            var tokenResponse = await _httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenRequest));

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("Feide token exchange failed: {Error}", errorContent);
                return BadRequest(new { error = "Failed to exchange authorization code" });
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<FeideTokenResponse>();
            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return BadRequest(new { error = "Invalid token response from Feide" });
            }

            // Get user info from Feide
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenData.AccessToken}");

            var userInfoEndpoint = _configuration["Feide:UserInfoEndpoint"] ?? "https://auth.dataporten.no/openid/userinfo";
            var userInfoResponse = await _httpClient.GetAsync(userInfoEndpoint);

            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Feide user info");
                return BadRequest(new { error = "Failed to retrieve user information" });
            }

            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<FeideUserInfo>();
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
            {
                return BadRequest(new { error = "Invalid user info from Feide" });
            }

            // Find or create user
            var user = await _userRepository.GetByEmailAsync(userInfo.Email);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            if (user == null)
            {
                // Auto-provision user (or return error based on your requirements)
                _logger.LogWarning("Feide user not found in system: {Email}", userInfo.Email);
                return Unauthorized(new { error = "User not registered in system" });
            }

            if (user.Status != UserStatus.Active)
            {
                _logger.LogWarning("Feide login attempt for inactive user: {Email}", userInfo.Email);
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
                _logger.LogWarning("Feide user {Email} has no organization assigned", user.Email);
                return Unauthorized(new { error = "User has no organization assigned" });
            }

            // Generate JWT tokens
            var roles = user.UserOrganizations
                .Select(uo => uo.Role.ToString())
                .Distinct()
                .ToList();

            var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, primaryOrganization.OrganizationId, roles);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

            await _jwtTokenService.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

            var accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "1440");
            var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes);

            // Audit successful login
            await _auditService.LogAuthenticationAsync(
                action: "FeideLoginSuccess",
                userId: user.Id,
                userEmail: user.Email,
                success: true,
                ipAddress: ipAddress,
                userAgent: userAgent);

            _logger.LogInformation("User logged in via Feide: {Email}", user.Email);

            var tokens = new AuthTokensResponse(accessToken, refreshToken, accessTokenExpiresAt);
            var userResponse = MapToUserResponse(user);
            var response = new LoginResponse(userResponse, tokens);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feide authentication error");
            return StatusCode(500, new { error = "Authentication failed" });
        }
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
            null,
            null,
            user.Phone,
            user.CreatedAt
        );
    }

    private class FeideTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
    }

    private class FeideUserInfo
    {
        public string Sub { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
    }
}

public record FeideCallbackRequest(
    string Code,
    string CodeVerifier,
    string RedirectUri
);
