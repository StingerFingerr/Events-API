using System.ComponentModel.DataAnnotations;
using Events.Domain.Consts;
using Events.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Events.Presentation.Middlewares;

public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleException(httpContext, ex);
        }
    }

    private async Task HandleException(HttpContext httpContext, Exception ex)
    {
        logger.LogError(
            ex,
            "Unhandled exception. Method={Method}, Path={Path}, RequestId={RequestId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Request.Headers["x-request-id"]);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var statusCode = MapStatusCode(ex);

        httpContext.Response.StatusCode = statusCode;

        var error = new ProblemDetails
        {
            Status = statusCode,
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? ErrorsMessages.InternalServerError
                : ex.Message
        };

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = error
            });
    }

    private static int MapStatusCode(Exception ex)
        => ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,

            NotFoundException => StatusCodes.Status404NotFound,



            ConflictException => StatusCodes.Status409Conflict,


            _ => StatusCodes.Status500InternalServerError
        };
}
