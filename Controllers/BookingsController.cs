using Events_API.DTOs.Bookings.Results;
using Events_API.Services.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Produces("application/json")]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("api/bookings/{id:guid}", Name = nameof(GetBooking))]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetBooking(Guid id)
    {
        var booking = await bookingService.GetBookingByIdAsync(id);
        return Ok(BookingDto.FromBooking(booking));
    }

    [HttpPost("api/events/{eventId:guid}/book")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingDto>> PostBooking(Guid eventId)
    {
        var booking = await bookingService.CreateBookingAsync(eventId);
        var bookingDto = BookingDto.FromBooking(booking);

        return AcceptedAtAction(nameof(GetBooking), new { id = bookingDto.Id }, bookingDto);
    }
}
