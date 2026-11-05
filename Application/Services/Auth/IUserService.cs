using Domain.Models;

namespace Application.Services.Auth;

public interface IUserService
{
    Task<Guid> RegisterAsync(string login, string password, UserRole role = UserRole.User);
    Task<string> LoginAsync(string login, string password);
}
