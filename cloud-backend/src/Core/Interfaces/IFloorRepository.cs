using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IFloorRepository : IRepository<Floor>
{
    Task<IEnumerable<Floor>> GetByBuildingIdAsync(Guid buildingId);
}
