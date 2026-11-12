namespace Users.Application.Services.Auth;

public interface IUserService
{
    Task<Guid> RegisterAsync(string login, string password);
    Task<string> LoginAsync(string login, string password);
}
