using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IOrganizationRepository : IRepository<Organization>
{
    Task<Organization?> GetBySlugAsync(string slug);
    Task<IEnumerable<Organization>> GetPagedAsync(int skip, int take);
    Task<int> GetCountAsync();
}
