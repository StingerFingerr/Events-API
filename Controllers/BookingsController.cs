using Application.DTOs.Bookings.Results;
using Application.Services.Bookings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Domain.Models;

namespace Events_API.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("bookings/{id:guid}")]
    [HttpGet("api/bookings/{id:guid}", Name = nameof(GetBooking))]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetBooking(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var booking = await bookingService.GetBookingByIdAsync(id, userId, CurrentRole);
        return Ok(booking);
    }

    [HttpPost("events/{eventId:guid}/book")]
    [HttpPost("api/events/{eventId:guid}/book")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingDto>> PostBooking(Guid eventId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var bookingDto = await bookingService.CreateBookingAsync(eventId, userId);

        return AcceptedAtAction(nameof(GetBooking), new { id = bookingDto.Id }, bookingDto);
    }

    [HttpDelete("bookings/{id:guid}")]
    [HttpDelete("api/bookings/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        await bookingService.CancelBookingAsync(id, userId, CurrentRole);
        return NoContent();
    }

    private UserRole CurrentRole => User.IsInRole(nameof(UserRole.Admin)) ? UserRole.Admin : UserRole.User;

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
