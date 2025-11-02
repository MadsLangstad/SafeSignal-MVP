using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IDeviceRepository : IRepository<Device>
{
    Task<Device?> GetByDeviceIdAsync(string deviceId);
    Task<IEnumerable<Device>> GetByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<Device>> GetPagedAsync(int skip, int take, Guid? organizationId = null);
    Task<int> GetCountAsync(Guid? organizationId = null);
}
