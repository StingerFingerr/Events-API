using Events_API.DTOs.Bookings.Results;
using Events_API.Services.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingsController(IBookingService bookingService) : ControllerBase
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
        var bookingDto = BookingDto.FromBooking(booking);

        return AcceptedAtAction(nameof(GetBooking), new { id = bookingDto.Id }, bookingDto);
    }
}
