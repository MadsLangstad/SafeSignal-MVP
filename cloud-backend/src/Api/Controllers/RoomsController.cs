using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : BaseAuthenticatedController
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBuildingRepository _buildingRepository;
    private readonly IFloorRepository _floorRepository;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomRepository roomRepository,
        IBuildingRepository buildingRepository,
        IFloorRepository floorRepository,
        ILogger<RoomsController> logger)
    {
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomResponse>>> GetRoomsByBuilding([FromQuery] Guid? buildingId, [FromQuery] Guid? floorId)
    {
        // Get authenticated user's organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        if (buildingId.HasValue)
        {
            // Validate that the building exists and belongs to the authenticated user's organization
            var building = await _buildingRepository.GetByIdAsync(buildingId.Value);
            if (building == null)
            {
                return NotFound(new { error = "Building not found" });
            }

            var buildingOrgId = building.Site?.OrganizationId ?? Guid.Empty;
            if (buildingOrgId == Guid.Empty || buildingOrgId != authenticatedOrgId)
            {
                return NotFound(new { error = "Building not found" }); // Return 404 to avoid leaking existence
            }

            var rooms = await _roomRepository.GetByBuildingIdAsync(buildingId.Value);
            return Ok(rooms.Select(MapToResponse));
        }

        if (floorId.HasValue)
        {
            // Validate that the floor's building belongs to the authenticated user's organization
            var floor = await _floorRepository.GetByIdAsync(floorId.Value);
            if (floor == null)
            {
                return NotFound(new { error = "Floor not found" });
            }

            var floorOrgId = floor.Building?.Site?.OrganizationId ?? Guid.Empty;
            if (floorOrgId == Guid.Empty || floorOrgId != authenticatedOrgId)
            {
                return NotFound(new { error = "Floor not found" }); // Return 404 to avoid leaking existence
            }

            var rooms = await _roomRepository.GetByFloorIdAsync(floorId.Value);
            return Ok(rooms.Select(MapToResponse));
        }

        return BadRequest(new { error = "Either buildingId or floorId query parameter is required" });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoomResponse>> GetRoom(Guid id)
    {
        var room = await _roomRepository.GetByIdAsync(id);

        if (room == null)
        {
            return NotFound(new { error = "Room not found" });
        }

        // Validate organization access via Floor -> Building -> Site -> Organization chain
        var organizationId = room.Floor?.Building?.Site?.OrganizationId;
        if (organizationId == null)
        {
            _logger.LogWarning("Room {RoomId} has incomplete organization chain", id);
            return BadRequest(new { error = "Room has incomplete organization data" });
        }

        try
        {
            ValidateOrganizationAccess(organizationId.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        return Ok(MapToResponse(room));
    }

    [HttpPost]
    public async Task<ActionResult<RoomResponse>> CreateRoom([FromBody] CreateRoomRequest request)
    {
        // Get authenticated user's organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        // Validate that the floor exists and its building belongs to the authenticated user's organization
        var floor = await _floorRepository.GetByIdAsync(request.FloorId);
        if (floor == null)
        {
            return NotFound(new { error = "Floor not found" });
        }

        var floorOrgId = floor.Building?.Site?.OrganizationId ?? Guid.Empty;
        if (floorOrgId == Guid.Empty || floorOrgId != authenticatedOrgId)
        {
            return NotFound(new { error = "Floor not found" }); // Return 404 to avoid leaking existence
        }

        // Check if room number already exists on this floor
        var existingRoom = await _roomRepository.GetByFloorAndRoomNumberAsync(request.FloorId, request.RoomNumber);
        if (existingRoom != null)
        {
            return BadRequest(new { error = "Room number already exists on this floor" });
        }

        var room = new Room
        {
            Id = Guid.NewGuid(),
            FloorId = request.FloorId,
            RoomNumber = request.RoomNumber,
            Name = request.Name,
            RoomType = request.RoomType,
            Capacity = request.Capacity
        };

        await _roomRepository.AddAsync(room);
        await _roomRepository.SaveChangesAsync();

        _logger.LogInformation("Room created: {RoomId} - {RoomNumber} in organization {OrganizationId}",
            room.Id, room.RoomNumber, authenticatedOrgId);

        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, MapToResponse(room));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RoomResponse>> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request)
    {
        var room = await _roomRepository.GetByIdAsync(id);

        if (room == null)
        {
            return NotFound(new { error = "Room not found" });
        }

        // Validate organization access before modification
        var organizationId = room.Floor?.Building?.Site?.OrganizationId;
        if (organizationId == null)
        {
            _logger.LogWarning("Room {RoomId} has incomplete organization chain", id);
            return BadRequest(new { error = "Room has incomplete organization data" });
        }

        try
        {
            ValidateOrganizationAccess(organizationId.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        // Check if updating room number conflicts with existing room
        if (request.RoomNumber != null && request.RoomNumber != room.RoomNumber)
        {
            var existingRoom = await _roomRepository.GetByFloorAndRoomNumberAsync(room.FloorId, request.RoomNumber);
            if (existingRoom != null && existingRoom.Id != id)
            {
                return BadRequest(new { error = "Room number already exists on this floor" });
            }
            room.RoomNumber = request.RoomNumber;
        }

        if (request.Name != null)
        {
            room.Name = request.Name;
        }

        if (request.RoomType != null)
        {
            room.RoomType = request.RoomType;
        }

        if (request.Capacity.HasValue)
        {
            room.Capacity = request.Capacity.Value;
        }

        await _roomRepository.UpdateAsync(room);
        await _roomRepository.SaveChangesAsync();

        _logger.LogInformation("Room updated: {RoomId}", id);

        return Ok(MapToResponse(room));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(Guid id)
    {
        var room = await _roomRepository.GetByIdAsync(id);

        if (room == null)
        {
            return NotFound(new { error = "Room not found" });
        }

        // Validate organization access before deletion
        var organizationId = room.Floor?.Building?.Site?.OrganizationId;
        if (organizationId == null)
        {
            _logger.LogWarning("Room {RoomId} has incomplete organization chain", id);
            return BadRequest(new { error = "Room has incomplete organization data" });
        }

        try
        {
            ValidateOrganizationAccess(organizationId.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        await _roomRepository.DeleteAsync(room);
        await _roomRepository.SaveChangesAsync();

        _logger.LogInformation("Room deleted: {RoomId}", id);

        return NoContent();
    }

    private static RoomResponse MapToResponse(Room room)
    {
        return new RoomResponse(
            room.Id.ToString(),
            room.Floor?.BuildingId.ToString() ?? Guid.Empty.ToString(),
            room.Name ?? room.RoomNumber ?? "Unnamed Room",
            room.Capacity,
            room.Floor?.FloorNumber.ToString()
        );
    }
}
