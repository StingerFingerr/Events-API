using Application.Repositories.Users;
using Domain.Exceptions;
using Domain.Models;

namespace Application.Services.Auth;

public class UserService(IUserRepository users, IPasswordHasher passwords, IJwtTokenService tokens)
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
