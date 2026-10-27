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
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddHostedService<BookingBackgroundService>();
        return services;
    }
}
