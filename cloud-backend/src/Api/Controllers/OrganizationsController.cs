using Microsoft.AspNetCore.Mvc;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationRepository organizationRepository,
        ILogger<OrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request)
    {
        // Check if slug already exists
        var existing = await _organizationRepository.GetBySlugAsync(request.Slug);
        if (existing != null)
        {
            return BadRequest(new { error = "Organization with this slug already exists" });
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Metadata = request.Metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = OrganizationStatus.Active
        };

        await _organizationRepository.AddAsync(organization);
        await _organizationRepository.SaveChangesAsync();

        _logger.LogInformation("Created organization {OrganizationId} with slug {Slug}",
            organization.Id, organization.Slug);

        var response = new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.CreatedAt,
            organization.UpdatedAt,
            organization.Status,
            organization.Metadata,
            0, // SiteCount
            0  // DeviceCount
        );

        return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(OrganizationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationListResponse>> ListOrganizations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var organizations = await _organizationRepository.GetPagedAsync(skip, pageSize);
        var totalCount = await _organizationRepository.GetCountAsync();

        var summaries = organizations.Select(o => new OrganizationSummary(
            o.Id,
            o.Name,
            o.Slug,
            o.Status,
            o.CreatedAt
        ));

        var response = new OrganizationListResponse(
            summaries,
            totalCount,
            page,
            pageSize
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationResponse>> GetOrganization(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return NotFound(new { error = "Organization not found" });
        }

        var response = new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.CreatedAt,
            organization.UpdatedAt,
            organization.Status,
            organization.Metadata,
            organization.Sites?.Count ?? 0,
            organization.Devices?.Count ?? 0
        );

        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganizationResponse>> UpdateOrganization(
        Guid id,
        [FromBody] UpdateOrganizationRequest request)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return NotFound(new { error = "Organization not found" });
        }

        // Check if new slug conflicts with existing organization
        if (!string.IsNullOrEmpty(request.Slug) && request.Slug != organization.Slug)
        {
            var existing = await _organizationRepository.GetBySlugAsync(request.Slug);
            if (existing != null)
            {
                return BadRequest(new { error = "Organization with this slug already exists" });
            }
            organization.Slug = request.Slug;
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            organization.Name = request.Name;
        }

        if (request.Status.HasValue)
        {
            organization.Status = request.Status.Value;
        }

        if (request.Metadata != null)
        {
            organization.Metadata = request.Metadata;
        }

        organization.UpdatedAt = DateTime.UtcNow;

        await _organizationRepository.UpdateAsync(organization);
        await _organizationRepository.SaveChangesAsync();

        _logger.LogInformation("Updated organization {OrganizationId}", organization.Id);

        var response = new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.CreatedAt,
            organization.UpdatedAt,
            organization.Status,
            organization.Metadata,
            organization.Sites?.Count ?? 0,
            organization.Devices?.Count ?? 0
        );

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrganization(Guid id)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            return NotFound(new { error = "Organization not found" });
        }

        // Soft delete
        organization.Status = OrganizationStatus.Deleted;
        organization.UpdatedAt = DateTime.UtcNow;

        await _organizationRepository.UpdateAsync(organization);
        await _organizationRepository.SaveChangesAsync();

        _logger.LogInformation("Deleted organization {OrganizationId}", organization.Id);

        return NoContent();
    }
}
