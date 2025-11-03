using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPhoneAsync(string phone);
    Task<IEnumerable<User>> GetByOrganizationIdAsync(Guid organizationId);
}
