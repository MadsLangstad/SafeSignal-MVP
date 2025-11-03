using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomRepository roomRepository, ILogger<RoomsController> logger)
    {
        _roomRepository = roomRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomResponse>>> GetRoomsByBuilding([FromQuery] Guid? buildingId, [FromQuery] Guid? floorId)
    {
        if (buildingId.HasValue)
        {
            var rooms = await _roomRepository.GetByBuildingIdAsync(buildingId.Value);
            return Ok(rooms.Select(MapToResponse));
        }

        if (floorId.HasValue)
        {
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

        return Ok(MapToResponse(room));
    }

    [HttpPost]
    public async Task<ActionResult<RoomResponse>> CreateRoom([FromBody] CreateRoomRequest request)
    {
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

        _logger.LogInformation("Room created: {RoomId} - {RoomNumber}", room.Id, room.RoomNumber);

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
