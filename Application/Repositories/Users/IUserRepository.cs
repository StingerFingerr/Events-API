using Domain.Models;

namespace Application.Repositories.Users;

public interface IUserRepository
{
    Task<User?> FindByLoginAsync(string login);
    Task AddAsync(User user);
}
