using Users.Domain.Models;

namespace Users.Application.Repositories.Users;

public interface IUserRepository
{
    Task<User?> FindByLoginAsync(string login);
    Task AddAsync(User user);
    Task UpgradePasswordHashAsync(Guid userId, string previousHash, string newHash);
}
