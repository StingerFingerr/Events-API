using Events_API.Controllers;
using Events_API.Background_tasks.Booking;
using Events_API.DTOs.Bookings.Results;
using Events_API.Models;
using Events_API.Services.Bookings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

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
            Status = BookingStatus.Pending
        };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.CreateBookingAsync(booking.EventId)).ReturnsAsync(booking);
        var queue = new Mock<IBookingTaskQueue>();
        var controller = new BookingsController(service.Object, queue.Object, NullLogger<BookingsController>.Instance);

        var response = await controller.PostBooking(booking.EventId);

        var result = Assert.IsType<AcceptedAtActionResult>(response.Result);
        var dto = Assert.IsType<BookingDto>(result.Value);
        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.Equal(nameof(BookingsController.GetBooking), result.ActionName);
        Assert.Equal(booking.Id, result.RouteValues!["id"]);
        Assert.Equal(booking.Id, dto.Id);
        Assert.Equal(booking.EventId, dto.EventId);
        Assert.Equal(BookingStatus.Pending, dto.Status);
        queue.Verify(q => q.Enqueue(It.Is<BookingTask>(task =>
            task.BookingId == booking.Id && task.EventId == booking.EventId)), Times.Once);
    }

    [Fact]
    public async Task GetBooking_ReturnsBookingDto()
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetBookingByIdAsync(booking.Id)).ReturnsAsync(booking);
        var controller = new BookingsController(
            service.Object,
            Mock.Of<IBookingTaskQueue>(),
            NullLogger<BookingsController>.Instance);

        var response = await controller.GetBooking(booking.Id);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<BookingDto>(result.Value);
        Assert.Equal(booking.Id, dto.Id);
        Assert.Equal(booking.EventId, dto.EventId);
        Assert.Equal(BookingStatus.Confirmed, dto.Status);
    }
}
