using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class RoomRepository : Repository<Room>, IRoomRepository
{
    public RoomRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Room>> GetByBuildingIdAsync(Guid buildingId)
    {
        return await _context.Rooms
            .Include(r => r.Floor)
            .Where(r => r.Floor!.BuildingId == buildingId)
            .OrderBy(r => r.Floor!.FloorNumber)
            .ThenBy(r => r.RoomNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<Room>> GetByFloorIdAsync(Guid floorId)
    {
        return await _context.Rooms
            .Where(r => r.FloorId == floorId)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync();
    }

    public async Task<Room?> GetByFloorAndRoomNumberAsync(Guid floorId, string roomNumber)
    {
        return await _context.Rooms
            .Include(r => r.Floor)
            .FirstOrDefaultAsync(r => r.FloorId == floorId && r.RoomNumber == roomNumber);
    }
}
