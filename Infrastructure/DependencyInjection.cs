using Events_API.Background_tasks.Booking;
using Events_API.DataAccess;
using Events_API.Repositories.Bookings;
using Events_API.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Events_API.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddHostedService<BookingBackgroundService>();
        return services;
    }
}
