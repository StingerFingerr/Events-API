using Events_API.Extensions;
using Events_API.Middlewares;
using Events_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IEventService, EventService>();  
builder.Services.AddSingleton<IEventsRepository, InMemoryEventsRepository>();
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true; 
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(); //fallback handler
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(); 
app.UseRouting();    
app.MapControllers();

app.Run();