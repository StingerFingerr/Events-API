using Microsoft.AspNetCore.Mvc;

namespace Events_API.Extensions;

public static class UserExtensions
{
    public static void AddControllersWithOptions(this IServiceCollection services)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;

                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(kv => kv.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value!.Errors.Select(e => e.ErrorMessage));

                    var customResponse = new
                    {
                        Message = "Ошибки валидации",
                        Errors = errors
                    };

                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();

                    var errorsString = string.Join(",", errors.Select(kv => $"{kv.Key}: {kv.Value}"));

                    logger.LogError($"Ошибка валидации: {errorsString}");

                    return new BadRequestObjectResult(customResponse);
                };
            }); 
    }
}