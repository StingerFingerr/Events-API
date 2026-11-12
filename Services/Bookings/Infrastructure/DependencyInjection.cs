using Bookings.Infrastructure.Auth;
using System.Text;
using Bookings.Infrastructure.Repositories.Bookings;
using Bookings.Infrastructure.DataAccess;
using Bookings.Application.Repositories.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BookingsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddOptions<JwtOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret must contain at least 32 UTF-8 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .ValidateOnStart();
        services.AddScoped<Bookings.Application.Services.Bookings.IBookingService, Bookings.Application.Services.Bookings.BookingsService>();
        return services;
    }
}
