using Contracts;
using Events.Application.Messaging;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Events.Presentation.Controllers;

[ApiController, Route("internal/messages"), ApiExplorerSettings(IgnoreApi = true)]
public sealed class MessagesController(ReservationSubscriber subscriber, IConfiguration settings) : ControllerBase
{
    private bool Authorized() => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(settings["Messaging:Key"]!),
        Encoding.UTF8.GetBytes(Request.Headers["X-Messaging-Key"].ToString()));

    [HttpPost(BookingTopics.ReserveSeats)]
    public async Task<IActionResult> Reserve(ReserveSeats message, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        if (message.SeatCount <= 0) return BadRequest();
        return Ok(await subscriber.Reserve(message, ct));
    }

    [HttpPost(BookingTopics.BookingConfirmed)]
    public async Task<IActionResult> Confirm(BookingConfirmed message, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        await subscriber.Confirm(message, ct);
        return NoContent();
    }

    [HttpPost(BookingTopics.BookingCancelled)]
    public async Task<IActionResult> Cancel(BookingCancelled message, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        await subscriber.Cancel(message, ct);
        return NoContent();
    }
}
