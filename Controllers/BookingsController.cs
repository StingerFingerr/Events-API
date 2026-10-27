using Application.DTOs.Bookings.Results;
using Application.Services.Bookings;
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
        return Ok(booking);
    }

    [HttpPost("api/events/{eventId:guid}/book")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingDto>> PostBooking(Guid eventId)
    {
        var bookingDto = await bookingService.CreateBookingAsync(eventId);

        return AcceptedAtAction(nameof(GetBooking), new { id = bookingDto.Id }, bookingDto);
    }
}
