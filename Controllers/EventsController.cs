using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetEvent))]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetEvent(int id)
    {
        var result = eventService.GetEventById(id);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType<List<EventDto>>(StatusCodes.Status200OK)]
    public IActionResult GetEvents([FromQuery] GetEventsByFiltersDto filters)
    {
        var result = eventService.GetEventsByFilters(filters);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public IActionResult PostEvent([FromBody] CreateEventDto eventData)
    {
        var result = eventService.CreateEvent(eventData);
        if (result.IsSuccess)
            return CreatedAtAction(
                nameof(GetEvent),
                new { id = result.Value!.Id },
                result.Value);
        return BadRequest(result.ErrorMessage);
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult PatchEvent(int id, [FromBody] UpdateTitleDto dto)
    {
        var result = eventService.UpdateEvent(id, dto.Title);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://ietf.org",
                Title = ErrorsMessages.CannotUpdateEventTitle,
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult PutEvent(int id, EventUpdateDto newEventData)
    {
        var result = eventService.UpdateEvent(id, newEventData);

        if (result.IsSuccess is false)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://ietf.org",
                Title = ErrorsMessages.CannotUpdateEventTitle,
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult DeleteEvent(int id)
    {
        var result = eventService.DeleteEvent(id);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://ietf.org",
                Title = ErrorsMessages.CannotDeleteEvent,
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return NoContent();
    }
}