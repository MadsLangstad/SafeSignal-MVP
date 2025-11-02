using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class AlertRepository : Repository<Alert>, IAlertRepository
{
    public AlertRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public async Task<Alert?> GetByAlertIdAsync(string alertId)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.AlertId == alertId);
    }

    public async Task<IEnumerable<Alert>> GetByOrganizationIdAsync(Guid organizationId, int skip, int take)
    {
        return await _dbSet
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.TriggeredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(Guid organizationId)
    {
        return await _dbSet.CountAsync(a => a.OrganizationId == organizationId);
    }
}
