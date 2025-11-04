using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : BaseAuthenticatedController
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAlertClearanceRepository _clearanceRepository;
    private readonly IBuildingRepository _buildingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertRepository alertRepository,
        IAlertClearanceRepository clearanceRepository,
        IBuildingRepository buildingRepository,
        IDeviceRepository deviceRepository,
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        IAuditService auditService,
        ILogger<AlertsController> logger)
    {
        _alertRepository = alertRepository;
        _clearanceRepository = clearanceRepository;
        _buildingRepository = buildingRepository;
        _deviceRepository = deviceRepository;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AlertResponse>> CreateAlert([FromBody] CreateAlertRequest request)
    {
        // Get authenticated user's organizationId - never trust client-provided organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        var existing = await _alertRepository.GetByAlertIdAsync(request.AlertId);
        if (existing != null)
        {
            return BadRequest(new { error = "Alert with this ID already exists" });
        }

        // Validate DeviceId ownership if provided
        if (request.DeviceId.HasValue)
        {
            var device = await _deviceRepository.GetByIdAsync(request.DeviceId.Value);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            if (device.OrganizationId != authenticatedOrgId)
            {
                return NotFound(new { error = "Device not found" }); // Return 404 to avoid leaking existence
            }
        }

        // Validate RoomId ownership if provided
        if (request.RoomId.HasValue)
        {
            var room = await _roomRepository.GetByIdAsync(request.RoomId.Value);
            if (room == null)
            {
                return NotFound(new { error = "Room not found" });
            }

            var roomOrgId = room.Floor?.Building?.Site?.OrganizationId;
            if (roomOrgId == null || roomOrgId != authenticatedOrgId)
            {
                return NotFound(new { error = "Room not found" }); // Return 404 to avoid leaking existence
            }
        }

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            AlertId = request.AlertId,
            OrganizationId = authenticatedOrgId,
            DeviceId = request.DeviceId,
            RoomId = request.RoomId,
            TriggeredAt = DateTime.UtcNow,
            Severity = request.Severity,
            AlertType = request.AlertType,
            Status = AlertStatus.New,
            Source = request.Source,
            Metadata = request.Metadata,
            CreatedAt = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(alert);
        await _alertRepository.SaveChangesAsync();

        _logger.LogInformation("Created alert {AlertId} for organization {OrganizationId}",
            alert.AlertId, alert.OrganizationId);

        return CreatedAtAction(nameof(GetAlert), new { id = alert.Id }, MapToResponse(alert));
    }

    [HttpPost("trigger")]
    public async Task<ActionResult<AlertResponse>> TriggerAlert([FromBody] TriggerAlertRequest request)
    {
        // Get authenticated user's organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        // Look up the building to get the OrganizationId from its site
        var building = await _buildingRepository.GetByIdAsync(request.BuildingId);
        if (building == null)
        {
            return NotFound(new { error = "Building not found" });
        }

        if (building.Site == null)
        {
            return BadRequest(new { error = "Building has no associated site" });
        }

        // Validate that the building belongs to the authenticated user's organization
        if (building.Site.OrganizationId != authenticatedOrgId)
        {
            return NotFound(new { error = "Building not found" }); // 404 to avoid leaking existence
        }

        // Validate DeviceId ownership if provided
        if (request.DeviceId.HasValue)
        {
            var device = await _deviceRepository.GetByIdAsync(request.DeviceId.Value);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            if (device.OrganizationId != authenticatedOrgId)
            {
                return NotFound(new { error = "Device not found" }); // Return 404 to avoid leaking existence
            }
        }

        // Validate RoomId ownership if provided
        if (request.RoomId.HasValue)
        {
            var room = await _roomRepository.GetByIdAsync(request.RoomId.Value);
            if (room == null)
            {
                return NotFound(new { error = "Room not found" });
            }

            var roomOrgId = room.Floor?.Building?.Site?.OrganizationId;
            if (roomOrgId == null || roomOrgId != authenticatedOrgId)
            {
                return NotFound(new { error = "Room not found" }); // Return 404 to avoid leaking existence
            }

            // Verify the room actually belongs to the specified building
            if (room.Floor?.BuildingId != request.BuildingId)
            {
                return BadRequest(new { error = "Room does not belong to the specified building" });
            }
        }

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            AlertId = Guid.NewGuid().ToString(),
            OrganizationId = building.Site.OrganizationId,
            BuildingId = request.BuildingId,
            DeviceId = request.DeviceId,
            RoomId = request.RoomId,
            TriggeredAt = DateTime.UtcNow,
            Severity = AlertSeverity.High,
            AlertType = request.Mode ?? "emergency",
            Status = AlertStatus.New,
            Source = AlertSource.Mobile,
            Metadata = request.Metadata,
            CreatedAt = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(alert);
        await _alertRepository.SaveChangesAsync();

        _logger.LogInformation("Alert triggered: {AlertId} for building {BuildingId} in organization {OrganizationId}",
            alert.AlertId, building.Id, alert.OrganizationId);

        return Ok(MapToResponse(alert));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertResponse>>> ListAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Get authenticated user's organizationId - never trust client-provided organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var alerts = await _alertRepository.GetByOrganizationIdAsync(authenticatedOrgId, skip, pageSize);

        return Ok(alerts.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertResponse>> GetAlert(Guid id)
    {
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            return NotFound();
        }

        // Validate organization access
        try
        {
            ValidateOrganizationAccess(alert.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(); // Return 404 to avoid leaking existence
        }

        return Ok(MapToResponse(alert));
    }

    [HttpPut("{id:guid}/acknowledge")]
    public async Task<ActionResult<AlertResponse>> AcknowledgeAlert(Guid id)
    {
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            return NotFound();
        }

        // Validate organization access before modification
        try
        {
            ValidateOrganizationAccess(alert.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        alert.Status = AlertStatus.Acknowledged;
        await _alertRepository.UpdateAsync(alert);
        await _alertRepository.SaveChangesAsync();

        _logger.LogInformation("Acknowledged alert {AlertId}", alert.AlertId);

        return Ok(MapToResponse(alert));
    }

    [HttpPut("{id:guid}/resolve")]
    public async Task<ActionResult<AlertResponse>> ResolveAlert(Guid id)
    {
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            return NotFound();
        }

        // Validate organization access before modification
        try
        {
            ValidateOrganizationAccess(alert.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        await _alertRepository.UpdateAsync(alert);
        await _alertRepository.SaveChangesAsync();

        _logger.LogInformation("Resolved alert {AlertId}", alert.AlertId);

        return Ok(MapToResponse(alert));
    }

    [HttpPost("{id:guid}/clear")]
    public async Task<ActionResult<ClearAlertResponse>> ClearAlert(Guid id, [FromBody] ClearAlertRequest request)
    {
        var authenticatedUserId = GetAuthenticatedUserId();
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        // Get alert with clearances
        var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
        if (alert == null)
        {
            return NotFound(new { error = "Alert not found" });
        }

        // Validate organization access
        try
        {
            ValidateOrganizationAccess(alert.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        // Get current user details
        var currentUser = await _userRepository.GetByIdAsync(authenticatedUserId);
        if (currentUser == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Check if alert is already resolved
        if (alert.Status == AlertStatus.Resolved)
        {
            return BadRequest(new { error = "Alert is already fully resolved" });
        }

        // Determine clearance step
        int clearanceStep;
        if (alert.Status == AlertStatus.New || alert.Status == AlertStatus.Acknowledged)
        {
            clearanceStep = 1;
        }
        else if (alert.Status == AlertStatus.PendingClearance)
        {
            clearanceStep = 2;

            // Prevent same user from clearing twice
            if (alert.FirstClearanceUserId == authenticatedUserId)
            {
                return BadRequest(new { error = "Cannot provide second clearance - you already provided the first clearance" });
            }
        }
        else
        {
            return BadRequest(new { error = $"Alert status {alert.Status} cannot be cleared" });
        }

        // Create clearance record
        var clearance = new AlertClearance
        {
            Id = Guid.NewGuid(),
            AlertId = alert.Id,
            UserId = authenticatedUserId,
            OrganizationId = authenticatedOrgId,
            ClearanceStep = clearanceStep,
            ClearedAt = DateTime.UtcNow,
            Notes = request.Notes,
            Location = request.Location != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Location)
                : null,
            DeviceInfo = Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await _clearanceRepository.AddAsync(clearance);

        // Update alert status and denormalized fields
        if (clearanceStep == 1)
        {
            alert.Status = AlertStatus.PendingClearance;
            alert.FirstClearanceUserId = authenticatedUserId;
            alert.FirstClearanceAt = clearance.ClearedAt;
        }
        else if (clearanceStep == 2)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.SecondClearanceUserId = authenticatedUserId;
            alert.SecondClearanceAt = clearance.ClearedAt;
            alert.FullyClearedAt = DateTime.UtcNow;
        }

        await _alertRepository.UpdateAsync(alert);
        await _alertRepository.SaveChangesAsync();
        await _clearanceRepository.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(new AuditLog
        {
            UserId = authenticatedUserId,
            OrganizationId = authenticatedOrgId,
            Action = $"Alert clearance step {clearanceStep}",
            EntityType = "Alert",
            EntityId = alert.Id,
            Category = AuditCategory.Alert,
            Success = true,
            AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(new
            {
                AlertId = alert.AlertId,
                ClearanceStep = clearanceStep,
                Status = alert.Status.ToString(),
                Notes = request.Notes
            })
        });

        _logger.LogInformation("Alert {AlertId} cleared by user {UserId} (step {Step})",
            alert.AlertId, authenticatedUserId, clearanceStep);

        // Build response
        ClearanceInfoDto? firstClearance = null;
        ClearanceInfoDto? secondClearance = null;

        if (alert.FirstClearanceUser != null)
        {
            firstClearance = new ClearanceInfoDto(
                alert.FirstClearanceUserId!.Value,
                $"{alert.FirstClearanceUser.FirstName} {alert.FirstClearanceUser.LastName}",
                alert.FirstClearanceAt!.Value
            );
        }

        if (alert.SecondClearanceUser != null)
        {
            secondClearance = new ClearanceInfoDto(
                alert.SecondClearanceUserId!.Value,
                $"{alert.SecondClearanceUser.FirstName} {alert.SecondClearanceUser.LastName}",
                alert.SecondClearanceAt!.Value
            );
        }

        return Ok(new ClearAlertResponse(
            alert.Id,
            alert.Status.ToString(),
            clearanceStep == 1
                ? "First clearance recorded. Awaiting second verification."
                : "Second clearance recorded. Alert fully resolved.",
            clearanceStep,
            clearance.Id,
            $"{currentUser.FirstName} {currentUser.LastName}",
            clearance.ClearedAt,
            clearanceStep == 1,
            firstClearance,
            secondClearance
        ));
    }

    [HttpGet("{id:guid}/clearances")]
    public async Task<ActionResult<AlertClearanceHistoryResponse>> GetClearances(Guid id)
    {
        var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
        if (alert == null)
        {
            return NotFound(new { error = "Alert not found" });
        }

        // Validate organization access
        try
        {
            ValidateOrganizationAccess(alert.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        var clearances = alert.Clearances
            .OrderBy(c => c.ClearanceStep)
            .Select(c => new AlertClearanceDto(
                c.Id,
                c.ClearanceStep,
                c.UserId,
                $"{c.User.FirstName} {c.User.LastName}",
                c.User.Email,
                c.ClearedAt,
                c.Notes,
                c.Location != null
                    ? System.Text.Json.JsonSerializer.Deserialize<LocationDto>(c.Location)
                    : null,
                c.DeviceInfo
            ))
            .ToList();

        return Ok(new AlertClearanceHistoryResponse(
            alert.Id,
            alert.Status.ToString(),
            clearances
        ));
    }

    private static AlertResponse MapToResponse(Alert alert) => new(
        alert.Id,
        alert.AlertId,
        alert.OrganizationId,
        alert.BuildingId,
        alert.DeviceId,
        alert.RoomId,
        alert.TriggeredAt,
        alert.ResolvedAt,
        alert.Severity,
        alert.AlertType,
        alert.Status,
        alert.Source,
        alert.Metadata,
        alert.CreatedAt
    );
}
