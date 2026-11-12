using Users.Application.Repositories.Users;
using Users.Domain.Exceptions;
using Users.Domain.Models;

namespace Users.Application.Services.Auth;

public class UserService(IUserRepository users, IPasswordHasher passwords, IJwtTokenService tokens) : IUserService
{
    public async Task<Guid> RegisterAsync(string login, string password)
    {
        var user = User.Create(Guid.NewGuid(), login, passwords.Hash(password));
        await users.AddAsync(user);
        return user.Id;
    }

    public async Task<string> LoginAsync(string login, string password)
    {
        var user = await users.FindByLoginAsync(login);
        if (user is null || !passwords.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();
        return tokens.Generate(user);
    }
}
