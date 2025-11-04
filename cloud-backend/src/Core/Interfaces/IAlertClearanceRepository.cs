using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IAlertClearanceRepository
{
    Task<AlertClearance?> GetByIdAsync(Guid id);
    Task<List<AlertClearance>> GetByAlertIdAsync(Guid alertId);
    Task<List<AlertClearance>> GetByOrganizationIdAsync(Guid organizationId, DateTime fromDate, DateTime toDate);
    Task AddAsync(AlertClearance clearance);
    Task SaveChangesAsync();
}
