using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(SafeSignalDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.UserOrganizations)
            .ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByPhoneAsync(string phone)
    {
        return await _context.Users
            .Include(u => u.UserOrganizations)
            .ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Phone == phone);
    }

    public async Task<IEnumerable<User>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.UserOrganizations
            .Where(uo => uo.OrganizationId == organizationId)
            .Include(uo => uo.User)
            .Select(uo => uo.User)
            .ToListAsync();
    }
}
