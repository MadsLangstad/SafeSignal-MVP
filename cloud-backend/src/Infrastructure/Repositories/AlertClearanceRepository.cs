using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class AlertClearanceRepository : IAlertClearanceRepository
{
    private readonly SafeSignalDbContext _context;

    public AlertClearanceRepository(SafeSignalDbContext context)
    {
        _context = context;
    }

    public async Task<AlertClearance?> GetByIdAsync(Guid id)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Include(c => c.Alert)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<AlertClearance>> GetByAlertIdAsync(Guid alertId)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Where(c => c.AlertId == alertId)
            .OrderBy(c => c.ClearanceStep)
            .ToListAsync();
    }

    public async Task<List<AlertClearance>> GetByOrganizationIdAsync(
        Guid organizationId, DateTime fromDate, DateTime toDate)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Include(c => c.Alert)
            .Where(c => c.OrganizationId == organizationId &&
                       c.ClearedAt >= fromDate &&
                       c.ClearedAt <= toDate)
            .OrderByDescending(c => c.ClearedAt)
            .ToListAsync();
    }

    public async Task AddAsync(AlertClearance clearance)
    {
        await _context.AlertClearances.AddAsync(clearance);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
