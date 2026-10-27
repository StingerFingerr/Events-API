using Domain.Consts;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Exceptions;
using Events_API.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Presentation.Tests;

public class ExceptionHandlingTests
{
    [Theory]
    [InlineData("validation", 400)]
    [InlineData("not-found", 404)]
    [InlineData("conflict", 409)]
    [InlineData("no-seats", 409)]
    [InlineData("unexpected", 500)]
    public async Task Middleware_MapsExceptionsToProblemDetails(string error, int expectedStatus)
    {
        Exception exception = error switch
        {
            "validation" => new ValidationException("Invalid request"),
            "not-found" => new NotFoundException(ErrorsMessages.EventNotFound),
            "conflict" => new ConflictException("Duplicate event"),
            "no-seats" => new NoAvailableSeatsException(),
            _ => new InvalidOperationException("Unexpected error")
        };
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => Task.FromException(exception), NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(exception.Message, json.RootElement.GetProperty("detail").GetString());
    }
}
