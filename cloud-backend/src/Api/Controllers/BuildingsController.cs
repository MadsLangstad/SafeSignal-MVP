using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using System.Security.Claims;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BuildingsController : ControllerBase
{
    private readonly IBuildingRepository _buildingRepository;
    private readonly ILogger<BuildingsController> _logger;

    public BuildingsController(IBuildingRepository buildingRepository, ILogger<BuildingsController> logger)
    {
        _buildingRepository = buildingRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BuildingResponse>>> ListBuildings(
        [FromQuery] Guid? organizationId = null)
    {
        // Get user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { error = "Invalid token claims" });
        }

        // For MVP, if organizationId is provided, use it
        // Otherwise, return all buildings (in production, filter by user's organization)
        IEnumerable<Building> buildings;
        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            buildings = await _buildingRepository.GetByOrganizationIdAsync(organizationId.Value);
        }
        else
        {
            // For MVP, return all buildings
            // TODO: In production, look up user's organization and filter
            buildings = await _buildingRepository.GetAllAsync();
        }

        return Ok(buildings.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BuildingResponse>> GetBuilding(Guid id)
    {
        var building = await _buildingRepository.GetByIdAsync(id);
        if (building == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(building));
    }

    [HttpPost]
    public async Task<ActionResult<BuildingResponse>> CreateBuilding([FromBody] CreateBuildingRequest request)
    {
        var building = new Building
        {
            Id = Guid.NewGuid(),
            SiteId = request.SiteId,
            Name = request.Name,
            Address = request.Address,
            FloorCount = request.FloorCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _buildingRepository.AddAsync(building);
        await _buildingRepository.SaveChangesAsync();

        _logger.LogInformation("Created building {BuildingId} for site {SiteId}",
            building.Id, building.SiteId);

        return CreatedAtAction(nameof(GetBuilding), new { id = building.Id }, MapToResponse(building));
    }

    private static BuildingResponse MapToResponse(Building building)
    {
        var rooms = building.Floors?
            .SelectMany(f => f.Rooms?.Select(r => new {Room = r, Floor = f}) ?? Enumerable.Empty<dynamic>())
            .Select(item => new RoomResponse(
                item.Room.Id.ToString(),
                building.Id.ToString(),
                item.Room.Name ?? item.Room.RoomNumber ?? "Unnamed Room",
                item.Room.Capacity,
                item.Floor.FloorNumber.ToString()
            ))
            .ToList() ?? new List<RoomResponse>();

        return new BuildingResponse(
            building.Id.ToString(),
            building.Site?.OrganizationId.ToString() ?? Guid.Empty.ToString(),
            building.Name ?? "Unnamed Building",
            building.Address ?? "",
            rooms
        );
    }
}
