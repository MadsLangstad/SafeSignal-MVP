using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IAlertRepository : IRepository<Alert>
{
    Task<Alert?> GetByAlertIdAsync(string alertId);
    Task<Alert?> GetByIdWithClearancesAsync(Guid id);
    Task<IEnumerable<Alert>> GetByOrganizationIdAsync(Guid organizationId, int skip, int take);
    Task<List<Alert>> GetPendingClearanceAlertsAsync(Guid organizationId, int skip, int take);
    Task<int> GetCountAsync(Guid organizationId);
}
