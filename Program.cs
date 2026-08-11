using Events_API.Extensions;
using Events_API.Middlewares;
using Events_API.Services.Bookings;
using Events_API.Services.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IEventService, EventsService>();
builder.Services.AddSingleton<IEventsRepository, InMemoryEventsRepository>();
builder.Services.AddSingleton<IBookingService, BookingsService>();
builder.Services.AddSingleton<IBookingsRepository, InMemoryBookingsRepository>();
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();

app.Run();
