using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;
using System.Security.Claims;

namespace SafeSignal.Cloud.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly SafeSignalDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, SafeSignalDbContext context, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid user ID in token" });
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var response = MapToUserResponse(user);
        return Ok(response);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserResponse>> UpdateCurrentUser([FromBody] UpdateUserRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid user ID in token" });
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Update user fields
        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            user.Phone = request.Phone;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User profile updated: {UserId}", userId);

        return Ok(MapToUserResponse(user));
    }

    [HttpPost("me/push-token")]
    public async Task<IActionResult> RegisterPushToken([FromBody] RegisterPushTokenRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid user ID in token" });
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Remove any existing push tokens for this user and platform (users can only have one active token per platform)
        var existingTokens = await _context.Set<PushToken>()
            .Where(pt => pt.UserId == userId.Value && pt.Platform == request.Platform && pt.RevokedAt == null)
            .ToListAsync();

        foreach (var existingToken in existingTokens)
        {
            existingToken.RevokedAt = DateTime.UtcNow;
        }

        // Store new push token
        var pushToken = new PushToken
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Token = request.Token,
            Platform = request.Platform,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<PushToken>().Add(pushToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Push token registered for user {UserId}: Platform={Platform}",
            userId, request.Platform);

        return Ok(new { message = "Push token registered successfully" });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
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
}
