using Events_API.Controllers;
using Events_API.DTOs.Bookings.Results;
using Events_API.Models;
using Events_API.Services.Bookings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Test;

public class BookingsControllerTests
{
    [Fact]
    public async Task PostBooking_ReturnsAcceptedDtoAndLocationForBooking()
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = BookingStatus.Pending
        };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.CreateBookingAsync(booking.EventId)).ReturnsAsync(booking);
        var controller = new BookingsController(service.Object);

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
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            Status = BookingStatus.Confirmed
        };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetBookingByIdAsync(booking.Id)).ReturnsAsync(booking);
        var controller = new BookingsController(service.Object);

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
