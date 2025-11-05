using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/auth/bankid")]
public class BankIDController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly ILogger<BankIDController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    // In-memory session storage (use Redis or database in production)
    private static readonly ConcurrentDictionary<string, BankIDSession> _sessions = new();

    public BankIDController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IAuditService auditService,
        ILogger<BankIDController> logger,
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

    [HttpPost("initiate")]
    public async Task<ActionResult<BankIDInitResponse>> Initiate([FromBody] BankIDInitRequest request)
    {
        try
        {
            var baseUrl = _configuration["BankID:BaseUrl"] ?? "https://appapi2.test.bankid.com/rp/v5.1";
            var certificatePath = _configuration["BankID:CertificatePath"];

            if (string.IsNullOrEmpty(certificatePath))
            {
                _logger.LogError("BankID configuration missing");
                return BadRequest(new { error = "BankID authentication not configured" });
            }

            // Prepare BankID auth request
            var authRequest = new
            {
                endUserIp = request.EndUserIp,
                personalNumber = request.PersonalNumber,
                requirement = new
                {
                    personalNumber = request.PersonalNumber
                }
            };

            // Call BankID auth endpoint
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/auth", authRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("BankID initiate failed: {Error}", errorContent);
                return BadRequest(new { error = "Failed to initiate BankID authentication" });
            }

            var bankIdResponse = await response.Content.ReadFromJsonAsync<BankIDAuthResponse>();
            if (bankIdResponse == null || string.IsNullOrEmpty(bankIdResponse.OrderRef))
            {
                return BadRequest(new { error = "Invalid response from BankID" });
            }

            // Store session
            var sessionId = Guid.NewGuid().ToString();
            var session = new BankIDSession
            {
                SessionId = sessionId,
                OrderRef = bankIdResponse.OrderRef,
                QrStartToken = bankIdResponse.QrStartToken,
                QrStartSecret = GenerateQrSecret(),
                CreatedAt = DateTime.UtcNow,
                Status = "pending"
            };

            _sessions.TryAdd(sessionId, session);

            // Generate QR code data and auto-start token
            var qrCodeData = GenerateQrCodeData(bankIdResponse.QrStartToken, session.QrStartSecret, 0);

            return Ok(new BankIDInitResponse
            {
                SessionId = sessionId,
                QrCodeData = qrCodeData,
                AutoStartToken = bankIdResponse.AutoStartToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BankID initiate error");
            return StatusCode(500, new { error = "Failed to initiate authentication" });
        }
    }

    [HttpGet("status/{sessionId}")]
    public async Task<ActionResult<BankIDStatusResponse>> GetStatus(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return NotFound(new { error = "Session not found" });
            }

            var baseUrl = _configuration["BankID:BaseUrl"] ?? "https://appapi2.test.bankid.com/rp/v5.1";

            // Collect status from BankID
            var collectRequest = new { orderRef = session.OrderRef };
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/collect", collectRequest);

            if (!response.IsSuccessStatusCode)
            {
                session.Status = "failed";
                return Ok(new BankIDStatusResponse
                {
                    Status = "failed",
                    HintCode = "collectionFailed"
                });
            }

            var collectResponse = await response.Content.ReadFromJsonAsync<BankIDCollectResponse>();
            if (collectResponse == null)
            {
                return BadRequest(new { error = "Invalid collect response" });
            }

            // Map BankID status to our status
            var status = MapBankIDStatus(collectResponse.Status);
            session.Status = status;

            if (collectResponse.Status == "complete" && collectResponse.CompletionData != null)
            {
                session.PersonalNumber = collectResponse.CompletionData.User?.PersonalNumber;
                session.Name = collectResponse.CompletionData.User?.Name;
            }

            return Ok(new BankIDStatusResponse
            {
                Status = status,
                HintCode = collectResponse.HintCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BankID status check error for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Status check failed" });
        }
    }

    [HttpPost("cancel/{sessionId}")]
    public async Task<IActionResult> Cancel(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return NotFound(new { error = "Session not found" });
            }

            var baseUrl = _configuration["BankID:BaseUrl"] ?? "https://appapi2.test.bankid.com/rp/v5.1";

            // Cancel BankID order
            var cancelRequest = new { orderRef = session.OrderRef };
            await _httpClient.PostAsJsonAsync($"{baseUrl}/cancel", cancelRequest);

            session.Status = "cancelled";
            _sessions.TryRemove(sessionId, out _);

            return Ok(new { message = "Session cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BankID cancel error for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Cancel failed" });
        }
    }

    [HttpPost("complete")]
    public async Task<ActionResult<LoginResponse>> Complete([FromBody] BankIDCompleteRequest request)
    {
        try
        {
            if (!_sessions.TryGetValue(request.SessionId, out var session))
            {
                return NotFound(new { error = "Session not found" });
            }

            if (session.Status != "complete")
            {
                return BadRequest(new { error = "Authentication not completed" });
            }

            if (string.IsNullOrEmpty(session.PersonalNumber))
            {
                return BadRequest(new { error = "Personal number not available" });
            }

            // Find user by personal number (assuming it's stored in a custom field or phone)
            // This is a simplified implementation - adjust based on your user model
            var user = await _userRepository.GetByPhoneAsync(session.PersonalNumber);

            if (user == null)
            {
                _logger.LogWarning("BankID user not found in system: {PersonalNumber}", session.PersonalNumber);
                return Unauthorized(new { error = "User not registered in system" });
            }

            if (user.Status != UserStatus.Active)
            {
                _logger.LogWarning("BankID login attempt for inactive user: {UserId}", user.Id);
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
                _logger.LogWarning("BankID user {UserId} has no organization assigned", user.Id);
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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            await _auditService.LogAuthenticationAsync(
                action: "BankIDLoginSuccess",
                userId: user.Id,
                userEmail: user.Email,
                success: true,
                ipAddress: ipAddress,
                userAgent: userAgent);

            _logger.LogInformation("User logged in via BankID: {Email}", user.Email);

            // Clean up session
            _sessions.TryRemove(request.SessionId, out _);

            var tokens = new AuthTokensResponse(accessToken, refreshToken, accessTokenExpiresAt);
            var userResponse = MapToUserResponse(user);
            var response = new LoginResponse(userResponse, tokens);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BankID complete error");
            return StatusCode(500, new { error = "Authentication completion failed" });
        }
    }

    private static string MapBankIDStatus(string bankIdStatus)
    {
        return bankIdStatus switch
        {
            "pending" => "pending",
            "complete" => "complete",
            "failed" => "failed",
            _ => "pending"
        };
    }

    private static string GenerateQrSecret()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateQrCodeData(string qrStartToken, string qrStartSecret, int secondsElapsed)
    {
        // Simplified QR generation - actual implementation needs HMAC
        return $"bankid.{qrStartToken}.{secondsElapsed}.{qrStartSecret}";
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

    private class BankIDSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string OrderRef { get; set; } = string.Empty;
        public string QrStartToken { get; set; } = string.Empty;
        public string QrStartSecret { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "pending";
        public string? PersonalNumber { get; set; }
        public string? Name { get; set; }
    }

    private class BankIDAuthResponse
    {
        public string OrderRef { get; set; } = string.Empty;
        public string AutoStartToken { get; set; } = string.Empty;
        public string QrStartToken { get; set; } = string.Empty;
        public string QrStartSecret { get; set; } = string.Empty;
    }

    private class BankIDCollectResponse
    {
        public string OrderRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? HintCode { get; set; }
        public BankIDCompletionData? CompletionData { get; set; }
    }

    private class BankIDCompletionData
    {
        public BankIDUser? User { get; set; }
    }

    private class BankIDUser
    {
        public string PersonalNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
    }
}

public record BankIDInitRequest(
    string EndUserIp,
    string? PersonalNumber = null
);

public record BankIDInitResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string QrCodeData { get; set; } = string.Empty;
    public string AutoStartToken { get; set; } = string.Empty;
}

public record BankIDStatusResponse
{
    public string Status { get; set; } = string.Empty;
    public string? HintCode { get; set; }
}

public record BankIDCompleteRequest(
    string SessionId
);
