using Bookings.Presentation.Controllers;
using Bookings.Application.DTOs.Bookings.Results;
using Bookings.Domain.Models;
using Bookings.Application.Services.Bookings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Presentation.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task PostBooking_ReturnsAcceptedDtoAndLocationForBooking()
    {
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid());
        var userId = Guid.NewGuid();
        var service = new Mock<IBookingService>();
        service.Setup(s => s.CreateBookingAsync(booking.EventId, userId)).ReturnsAsync(BookingDto.FromBooking(booking));
        var controller = new BookingsController(service.Object);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", userId.ToString())], "Bearer"));
        var response = await controller.PostBooking(booking.EventId);

        var result = Assert.IsType<AcceptedAtActionResult>(response.Result);
        var dto = Assert.IsType<BookingDto>(result.Value);
        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.Equal(nameof(BookingsController.GetBooking), result.ActionName);
        Assert.Equal(booking.Id, result.RouteValues!["id"]);
        Assert.Equal(booking.Id, dto.Id);
        Assert.Equal(booking.EventId, dto.EventId);
        Assert.Equal(booking.CreatedAt, dto.CreatedAt);
        Assert.Equal(booking.ProcessedAt, dto.ProcessedAt);
        Assert.Equal(BookingStatus.Pending, dto.Status);
    }

    [Fact]
    public async Task GetBooking_ReturnsBookingDto()
    {
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid());
        booking.ApplyDecision(true);
        var service = new Mock<IBookingService>();
        var userId = Guid.NewGuid();
        service.Setup(s => s.GetBookingByIdAsync(booking.Id, userId, false)).ReturnsAsync(BookingDto.FromBooking(booking));
        var controller = new BookingsController(service.Object);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", userId.ToString())], "Bearer"));
        var response = await controller.GetBooking(booking.Id);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<BookingDto>(result.Value);
        Assert.Equal(booking.Id, dto.Id);
        Assert.Equal(booking.EventId, dto.EventId);
        Assert.Equal(booking.CreatedAt, dto.CreatedAt);
        Assert.Equal(booking.ProcessedAt, dto.ProcessedAt);
        Assert.Equal(BookingStatus.Confirmed, dto.Status);
    }
}
