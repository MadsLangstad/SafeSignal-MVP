using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : BaseAuthenticatedController
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceRepository deviceRepository,
        IRoomRepository roomRepository,
        ILogger<DevicesController> logger)
    {
        _deviceRepository = deviceRepository;
        _roomRepository = roomRepository;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceResponse>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        // Get authenticated user's organizationId - never trust client-provided organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        var existing = await _deviceRepository.GetByDeviceIdAsync(request.DeviceId);
        if (existing != null)
        {
            return BadRequest(new { error = "Device already registered" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            OrganizationId = authenticatedOrgId,
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

        _logger.LogInformation("Registered device {DeviceId} for organization {OrganizationId}",
            device.DeviceId, device.OrganizationId);

        return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, MapToResponse(device));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceResponse>>> ListDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Get authenticated user's organizationId - never trust client input
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var devices = await _deviceRepository.GetPagedAsync(skip, pageSize, authenticatedOrgId);

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

        // Validate organization access
        try
        {
            ValidateOrganizationAccess(device.OrganizationId);
        }
        catch (UnauthorizedAccessException)
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

        // Validate organization access before modification
        try
        {
            ValidateOrganizationAccess(device.OrganizationId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
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
            if (roomOrgId == null || roomOrgId != device.OrganizationId)
            {
                return NotFound(new { error = "Room not found" }); // Return 404 to avoid leaking existence
            }

            device.RoomId = request.RoomId.Value;
        }

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
