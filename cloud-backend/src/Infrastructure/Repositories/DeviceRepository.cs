using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class DeviceRepository : Repository<Device>, IDeviceRepository
{
    public DeviceRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public async Task<Device?> GetByDeviceIdAsync(string deviceId)
    {
        return await _dbSet.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
    }

    public async Task<IEnumerable<Device>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _dbSet.Where(d => d.OrganizationId == organizationId).ToListAsync();
    }

    public async Task<IEnumerable<Device>> GetPagedAsync(int skip, int take, Guid? organizationId = null)
    {
        var query = _dbSet.AsQueryable();

        if (organizationId.HasValue)
        {
            query = query.Where(d => d.OrganizationId == organizationId.Value);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(Guid? organizationId = null)
    {
        if (organizationId.HasValue)
        {
            return await _dbSet.CountAsync(d => d.OrganizationId == organizationId.Value);
        }
        return await _dbSet.CountAsync();
    }
}
