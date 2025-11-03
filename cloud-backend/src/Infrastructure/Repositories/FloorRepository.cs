using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class FloorRepository : Repository<Floor>, IFloorRepository
{
    public FloorRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public override async Task<Floor?> GetByIdAsync(Guid id)
    {
        return await _context.Floors
            .Include(f => f.Building)
                .ThenInclude(b => b.Site)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<IEnumerable<Floor>> GetByBuildingIdAsync(Guid buildingId)
    {
        return await _context.Floors
            .Include(f => f.Building)
                .ThenInclude(b => b.Site)
            .Where(f => f.BuildingId == buildingId)
            .ToListAsync();
    }
}
