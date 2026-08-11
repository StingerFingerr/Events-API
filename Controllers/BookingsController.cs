using Events_API.Background_tasks.Booking;
using Events_API.DTOs.Bookings.Results;
using Events_API.Services.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingsController(IBookingService bookingService, IBookingTaskQueue bookingTaskQueue, ILogger<BookingsController> logger) : ControllerBase
{
    [HttpGet("{id:guid}", Name = nameof(GetBooking))]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetBooking(Guid id)
    {
        var booking = await bookingService.GetBookingByIdAsync(id);
        return Ok(BookingDto.FromBooking(booking));
    }

    [HttpPost("{eventId:guid}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingDto>> PostBooking(Guid eventId)
    {
        var booking = await bookingService.CreateBookingAsync(eventId);
        bookingTaskQueue.Enqueue(new BookingTask
        {
            BookingId = booking.Id,
            EventId = booking.EventId
        });
        logger.LogInformation("Booking {BookingId} was queued for processing", booking.Id);

        var bookingDto = BookingDto.FromBooking(booking);

        return AcceptedAtAction(nameof(GetBooking), new { id = bookingDto.Id }, bookingDto);
    }
}
