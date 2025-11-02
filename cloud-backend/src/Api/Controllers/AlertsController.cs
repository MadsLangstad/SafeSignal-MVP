using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertRepository alertRepository, ILogger<AlertsController> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AlertResponse>> CreateAlert([FromBody] CreateAlertRequest request)
    {
        var existing = await _alertRepository.GetByAlertIdAsync(request.AlertId);
        if (existing != null)
        {
            return BadRequest(new { error = "Alert with this ID already exists" });
        }

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            AlertId = request.AlertId,
            OrganizationId = request.OrganizationId,
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertResponse>>> ListAlerts(
        [FromQuery] Guid organizationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var alerts = await _alertRepository.GetByOrganizationIdAsync(organizationId, skip, pageSize);

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
