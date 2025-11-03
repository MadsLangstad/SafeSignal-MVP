using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IRoomRepository : IRepository<Room>
{
    Task<IEnumerable<Room>> GetByBuildingIdAsync(Guid buildingId);
    Task<IEnumerable<Room>> GetByFloorIdAsync(Guid floorId);
    Task<Room?> GetByFloorAndRoomNumberAsync(Guid floorId, string roomNumber);
}
