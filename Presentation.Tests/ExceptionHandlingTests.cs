using Domain.Consts;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Exceptions;
using Events_API.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Presentation.Tests;

public class ExceptionHandlingTests
{
    [Theory]
    [InlineData("validation", 400)]
    [InlineData("not-found", 404)]
    [InlineData("conflict", 409)]
    [InlineData("no-seats", 409)]
    [InlineData("past-event", 400)]
    [InlineData("invalid-credentials", 400)]
    [InlineData("insufficient-permissions", 403)]
    [InlineData("booking-limit", 409)]
    [InlineData("unexpected", 500)]
    public async Task Middleware_MapsExceptionsToProblemDetails(string error, int expectedStatus)
    {
        Exception exception = error switch
        {
            "validation" => new ValidationException("Invalid request"),
            "not-found" => new NotFoundException(ErrorsMessages.EventNotFound),
            "conflict" => new ConflictException("Duplicate event"),
            "no-seats" => new NoAvailableSeatsException(),
            "past-event" => new PastEventBookingException(),
            "invalid-credentials" => new InvalidCredentialsException(),
            "insufficient-permissions" => new InsufficientPermissionsException(),
            "booking-limit" => new ActiveBookingLimitExceededException(10),
            _ => new InvalidOperationException("Unexpected error")
        };
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        context.RequestServices = provider;
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new Mock<ILogger<GlobalExceptionHandlingMiddleware>>();
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => Task.FromException(exception), logger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("type").GetString()));
        var expectedDetail = expectedStatus >= 500 ? ErrorsMessages.InternalServerError : exception.Message;
        Assert.Equal(expectedDetail, json.RootElement.GetProperty("detail").GetString());
        if (expectedStatus >= 500)
            Assert.DoesNotContain(exception.Message, json.RootElement.GetRawText());

        logger.Verify(log => log.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
