using Domain.Models;

namespace Application.Services.Auth;

public interface IJwtTokenService
{
    string Generate(User user);
}
