using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceRepository deviceRepository, ILogger<DevicesController> logger)
    {
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceResponse>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var existing = await _deviceRepository.GetByDeviceIdAsync(request.DeviceId);
        if (existing != null)
        {
            return BadRequest(new { error = "Device already registered" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            OrganizationId = request.OrganizationId,
            DeviceType = DeviceType.Button,
            SerialNumber = request.SerialNumber,
            MacAddress = request.MacAddress,
            HardwareVersion = request.HardwareVersion,
            Metadata = request.Metadata,
            Status = DeviceStatus.Inactive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _deviceRepository.AddAsync(device);
        await _deviceRepository.SaveChangesAsync();

        _logger.LogInformation("Registered device {DeviceId}", device.DeviceId);

        return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, MapToResponse(device));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceResponse>>> ListDevices(
        [FromQuery] Guid? organizationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var devices = await _deviceRepository.GetPagedAsync(skip, pageSize, organizationId);

        return Ok(devices.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeviceResponse>> GetDevice(Guid id)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(device));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeviceResponse>> UpdateDevice(Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        if (request.RoomId.HasValue) device.RoomId = request.RoomId.Value;
        if (request.FirmwareVersion != null) device.FirmwareVersion = request.FirmwareVersion;
        if (request.Status.HasValue) device.Status = request.Status.Value;
        if (request.Metadata != null) device.Metadata = request.Metadata;

        device.UpdatedAt = DateTime.UtcNow;
        device.LastSeenAt = DateTime.UtcNow;

        await _deviceRepository.UpdateAsync(device);
        await _deviceRepository.SaveChangesAsync();

        return Ok(MapToResponse(device));
    }

    private static DeviceResponse MapToResponse(Device device) => new(
        device.Id,
        device.DeviceId,
        device.OrganizationId,
        device.RoomId,
        device.DeviceType,
        device.FirmwareVersion,
        device.HardwareVersion,
        device.SerialNumber,
        device.MacAddress,
        device.ProvisionedAt,
        device.LastSeenAt,
        device.Status,
        device.CreatedAt,
        device.UpdatedAt,
        device.Metadata
    );
}
