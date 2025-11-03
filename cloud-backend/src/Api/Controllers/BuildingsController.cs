using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using System.Security.Claims;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BuildingsController : BaseAuthenticatedController
{
    private readonly IBuildingRepository _buildingRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly ILogger<BuildingsController> _logger;

    public BuildingsController(
        IBuildingRepository buildingRepository,
        ISiteRepository siteRepository,
        ILogger<BuildingsController> logger)
    {
        _buildingRepository = buildingRepository;
        _siteRepository = siteRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BuildingResponse>>> ListBuildings()
    {
        // Get authenticated user's organizationId from JWT
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        // Only return buildings for the authenticated user's organization
        var buildings = await _buildingRepository.GetByOrganizationIdAsync(authenticatedOrgId);

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

        // Validate that the building belongs to the authenticated user's organization
        var buildingOrgId = building.Site?.OrganizationId ?? Guid.Empty;
        if (buildingOrgId == Guid.Empty)
        {
            return BadRequest(new { error = "Building has no associated organization" });
        }

        try
        {
            ValidateOrganizationAccess(buildingOrgId);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(); // Return 404 instead of 403 to avoid leaking existence
        }

        return Ok(MapToResponse(building));
    }

    [HttpPost]
    public async Task<ActionResult<BuildingResponse>> CreateBuilding([FromBody] CreateBuildingRequest request)
    {
        // Get authenticated user's organizationId
        var authenticatedOrgId = GetAuthenticatedOrganizationId();

        // Validate that the site exists and belongs to the authenticated user's organization
        var site = await _siteRepository.GetByIdAsync(request.SiteId);
        if (site == null)
        {
            return NotFound(new { error = "Site not found" });
        }

        if (site.OrganizationId != authenticatedOrgId)
        {
            return NotFound(new { error = "Site not found" }); // Return 404 to avoid leaking existence
        }

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

        _logger.LogInformation("Created building {BuildingId} for site {SiteId} in organization {OrganizationId}",
            building.Id, building.SiteId, authenticatedOrgId);

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
