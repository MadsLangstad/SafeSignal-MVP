using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface ISiteRepository : IRepository<Site>
{
    Task<IEnumerable<Site>> GetByOrganizationIdAsync(Guid organizationId);
}
