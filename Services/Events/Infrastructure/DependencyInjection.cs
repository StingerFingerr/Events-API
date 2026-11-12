using Events.Infrastructure.Auth;
using System.Text;
using Events.Infrastructure.Repositories.Events;
using Events.Infrastructure.DataAccess;
using Events.Application.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EventsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddOptions<JwtOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret must contain at least 32 UTF-8 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.LifetimeMinutes > 0, "Jwt:LifetimeMinutes must be positive.")
            .ValidateOnStart();
        services.AddScoped<Events.Application.Services.Events.IEventService, Events.Application.Services.Events.EventsService>();
        return services;
    }
}
