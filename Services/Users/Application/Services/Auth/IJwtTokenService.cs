using Users.Domain.Models;

namespace Users.Application.Services.Auth;

public interface IJwtTokenService
{
    string Generate(User user);
}
