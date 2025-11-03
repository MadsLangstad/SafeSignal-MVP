using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class SiteRepository : Repository<Site>, ISiteRepository
{
    public SiteRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public override async Task<Site?> GetByIdAsync(Guid id)
    {
        return await _context.Sites
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Site>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Sites
            .Include(s => s.Organization)
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync();
    }
}
