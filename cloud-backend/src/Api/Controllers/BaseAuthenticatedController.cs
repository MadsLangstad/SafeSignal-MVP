using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SafeSignal.Cloud.Api.Controllers;

[ApiController]
[Authorize]
public abstract class BaseAuthenticatedController : ControllerBase
{
    protected Guid GetAuthenticatedOrganizationId()
    {
        var organizationIdClaim = User.FindFirst("organizationId");
        if (organizationIdClaim == null || !Guid.TryParse(organizationIdClaim.Value, out var organizationId))
        {
            throw new UnauthorizedAccessException("Invalid or missing organizationId in token");
        }
        return organizationId;
    }

    protected Guid GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing userId in token");
        }
        return userId;
    }

    protected void ValidateOrganizationAccess(Guid resourceOrganizationId)
    {
        var authenticatedOrganizationId = GetAuthenticatedOrganizationId();
        if (resourceOrganizationId != authenticatedOrganizationId)
        {
            throw new UnauthorizedAccessException($"Access denied to resource from organization {resourceOrganizationId}");
        }
    }

    protected bool HasRole(string role)
    {
        return User.IsInRole(role);
    }

    protected bool IsOrgAdmin()
    {
        return User.IsInRole("SuperAdmin") || User.IsInRole("OrgAdmin");
    }

    protected bool IsSuperAdmin()
    {
        return User.IsInRole("SuperAdmin");
    }

    protected void RequireOrgAdmin()
    {
        if (!IsOrgAdmin())
        {
            throw new UnauthorizedAccessException("Organization administrator privileges required");
        }
    }

    protected void RequireSuperAdmin()
    {
        if (!IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Platform super administrator privileges required");
        }
    }
}
