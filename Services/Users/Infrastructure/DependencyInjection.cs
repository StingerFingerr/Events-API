using Users.Application.Services.Auth;
using Users.Infrastructure.Auth;
using Users.Application.Repositories.Users;
using Users.Infrastructure.Repositories.Users;
using System.Text;
using Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddOptions<JwtOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret must contain at least 32 UTF-8 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.LifetimeMinutes > 0, "Jwt:LifetimeMinutes must be positive.")
            .ValidateOnStart();
        services.AddScoped<Users.Application.Services.Auth.IUserService, Users.Application.Services.Auth.UserService>();
        return services;
    }
}
