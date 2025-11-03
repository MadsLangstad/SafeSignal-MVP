using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Query audit logs with optional filters
    /// </summary>
    /// <param name="category">Filter by audit category</param>
    /// <param name="startDate">Start date for audit logs (ISO 8601)</param>
    /// <param name="endDate">End date for audit logs (ISO 8601)</param>
    /// <param name="limit">Maximum number of results (default: 100, max: 1000)</param>
    /// <returns>List of audit log entries</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    public async Task<ActionResult<List<AuditLog>>> GetAuditLogs(
        [FromQuery] AuditCategory? category = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        // Get organization ID from JWT claims
        var organizationIdClaim = User.FindFirst("organizationId")?.Value;
        if (!Guid.TryParse(organizationIdClaim, out var organizationId))
        {
            _logger.LogWarning("Invalid organization ID in token");
            return Unauthorized(new { error = "Invalid organization context" });
        }

        // Limit max results to prevent abuse
        limit = Math.Min(limit, 1000);

        var auditLogs = await _auditService.QueryAsync(
            userId: null, // Don't filter by user initially
            organizationId: organizationId,
            category: category,
            startDate: startDate,
            endDate: endDate,
            limit: limit
        );

        return Ok(auditLogs);
    }

    /// <summary>
    /// Get audit logs for current user
    /// </summary>
    /// <param name="category">Filter by audit category</param>
    /// <param name="startDate">Start date (ISO 8601)</param>
    /// <param name="endDate">End date (ISO 8601)</param>
    /// <param name="limit">Maximum number of results (default: 100)</param>
    [HttpGet("me")]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    public async Task<ActionResult<List<AuditLog>>> GetMyAuditLogs(
        [FromQuery] AuditCategory? category = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid user ID in token" });
        }

        limit = Math.Min(limit, 1000);

        var auditLogs = await _auditService.QueryAsync(
            userId: userId,
            organizationId: null,
            category: category,
            startDate: startDate,
            endDate: endDate,
            limit: limit
        );

        return Ok(auditLogs);
    }

    /// <summary>
    /// Get security events for organization (Admin only)
    /// </summary>
    [HttpGet("security")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    public async Task<ActionResult<List<AuditLog>>> GetSecurityEvents(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        var organizationIdClaim = User.FindFirst("organizationId")?.Value;
        if (!Guid.TryParse(organizationIdClaim, out var organizationId))
        {
            return Unauthorized(new { error = "Invalid organization context" });
        }

        limit = Math.Min(limit, 1000);

        var auditLogs = await _auditService.QueryAsync(
            userId: null,
            organizationId: organizationId,
            category: AuditCategory.Security,
            startDate: startDate,
            endDate: endDate,
            limit: limit
        );

        return Ok(auditLogs);
    }
}
