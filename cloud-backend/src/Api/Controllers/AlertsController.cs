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
    private readonly IBuildingRepository _buildingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertRepository alertRepository,
        IBuildingRepository buildingRepository,
        IDeviceRepository deviceRepository,
        IRoomRepository roomRepository,
        ILogger<AlertsController> logger)
    {
        _alertRepository = alertRepository;
        _buildingRepository = buildingRepository;
        _deviceRepository = deviceRepository;
        _roomRepository = roomRepository;
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
