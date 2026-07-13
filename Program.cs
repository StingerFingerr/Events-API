using Events_API.Extensions;
using Events_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IEventService, EventService>();  

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSwagger();
app.UseSwaggerUI(); 
app.UseRouting();    
app.MapControllers();

app.Run();