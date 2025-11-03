using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class BuildingRepository : Repository<Building>, IBuildingRepository
{
    public BuildingRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public override async Task<Building?> GetByIdAsync(Guid id)
    {
        return await _context.Buildings
            .Include(b => b.Site)
            .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public override async Task<IEnumerable<Building>> GetAllAsync()
    {
        return await _context.Buildings
            .Include(b => b.Site)
            .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
            .ToListAsync();
    }

    public async Task<IEnumerable<Building>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Buildings
            .Include(b => b.Site)
            .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
            .Where(b => b.Site.OrganizationId == organizationId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Building>> GetBySiteIdAsync(Guid siteId)
    {
        return await _context.Buildings
            .Include(b => b.Site)
            .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
            .Where(b => b.SiteId == siteId)
            .ToListAsync();
    }
}
