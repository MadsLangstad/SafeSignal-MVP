using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
{
    public OrganizationRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public async Task<Organization?> GetBySlugAsync(string slug)
    {
        return await _dbSet
            .FirstOrDefaultAsync(o => o.Slug == slug);
    }

    public async Task<IEnumerable<Organization>> GetPagedAsync(int skip, int take)
    {
        return await _dbSet
            .OrderBy(o => o.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public override async Task<Organization?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(o => o.Sites)
            .Include(o => o.Devices)
            .FirstOrDefaultAsync(o => o.Id == id);
    }
}
