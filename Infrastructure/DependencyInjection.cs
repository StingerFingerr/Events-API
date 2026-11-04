using Application.Services.Auth;
using Infrastructure.Auth;
using Application.Repositories.Users;
using Infrastructure.Repositories.Users;
using System.Text;
using Infrastructure.Repositories.Bookings;
using Infrastructure.Repositories.Events;
using Infrastructure.BackgroundTasks.Booking;
using Infrastructure.DataAccess;
using Application.Repositories.Bookings;
using Application.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddOptions<JwtOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret must contain at least 32 UTF-8 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.LifetimeMinutes > 0, "Jwt:LifetimeMinutes must be positive.");
        services.AddHostedService<BookingBackgroundService>();
        return services;
    }
}
