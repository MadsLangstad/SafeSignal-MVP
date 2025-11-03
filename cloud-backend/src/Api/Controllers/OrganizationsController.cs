using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : BaseAuthenticatedController
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request)
    {
        // Only SuperAdmins can create new organizations
        try
        {
            RequireSuperAdmin();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }

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
        // Only return the authenticated user's organization - no listing other orgs
        var authenticatedOrgId = GetAuthenticatedOrganizationId();
        var organization = await _organizationRepository.GetByIdAsync(authenticatedOrgId);

        if (organization == null)
        {
            return NotFound(new { error = "Organization not found" });
        }

        var summary = new OrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Status,
            organization.CreatedAt
        );

        var response = new OrganizationListResponse(
            new[] { summary },
            1, // Only one organization
            1, // Always page 1
            1  // Always page size 1
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationResponse>> GetOrganization(Guid id)
    {
        // Validate that the requested organization matches the authenticated user's organization
        try
        {
            ValidateOrganizationAccess(id);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { error = "Organization not found" });
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationResponse>> UpdateOrganization(
        Guid id,
        [FromBody] UpdateOrganizationRequest request)
    {
        // Validate organization access before modification
        try
        {
            ValidateOrganizationAccess(id);
            RequireOrgAdmin(); // Only org admins can update organization settings
        }
        catch (UnauthorizedAccessException ex)
        {
            if (ex.Message.Contains("administrator privileges"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            return NotFound(new { error = "Organization not found" });
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteOrganization(Guid id)
    {
        // Validate organization access before deletion
        try
        {
            ValidateOrganizationAccess(id);
            RequireOrgAdmin(); // Only org admins can delete organization
        }
        catch (UnauthorizedAccessException ex)
        {
            if (ex.Message.Contains("administrator privileges"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            return NotFound(new { error = "Organization not found" });
        }

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
