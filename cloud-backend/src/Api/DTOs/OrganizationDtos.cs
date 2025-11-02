using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Api.DTOs;

public record CreateOrganizationRequest(
    string Name,
    string Slug,
    string? Metadata
);

public record UpdateOrganizationRequest(
    string? Name,
    string? Slug,
    OrganizationStatus? Status,
    string? Metadata
);

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    OrganizationStatus Status,
    string? Metadata,
    int SiteCount,
    int DeviceCount
);

public record OrganizationListResponse(
    IEnumerable<OrganizationSummary> Organizations,
    int TotalCount,
    int Page,
    int PageSize
);

public record OrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    OrganizationStatus Status,
    DateTime CreatedAt
);
