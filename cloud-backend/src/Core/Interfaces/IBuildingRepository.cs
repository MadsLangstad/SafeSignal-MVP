using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IBuildingRepository : IRepository<Building>
{
    Task<IEnumerable<Building>> GetByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<Building>> GetBySiteIdAsync(Guid siteId);
}
