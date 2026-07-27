using Events_API.Consts;
using Events_API.DTOs.Events;
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
    public async Task<IActionResult> GetEvent(int id)
    {
        var result = await eventService.GetEventByIdAsync(id);
        if(result is null)
            return NotFound();
        return  Ok(result);
    }

    [HttpGet]
    [ProducesResponseType<List<EventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllEvents()
    {
        var result = await eventService.GetAllEventsAsync();
        return Ok(result);
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostEvent([FromBody] CreateEventDto eventData)
    {
        var result = await eventService.CreateEventAsync(eventData);
        if(result.IsSuccess)
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
    public async Task<IActionResult> PatchEvent(int id, [FromBody] UpdateTitleDto dto)
    {
        var result = await eventService.UpdateEventAsync(id, dto.Title);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.ErrorMessage, ErrorsMessages.EventNotFound, StringComparison.Ordinal))
                return NotFound();

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
    public async Task<IActionResult> PutEvent(int id, EventUpdateDto newEventData)
    {
        var result = await eventService.UpdateEventAsync(id, newEventData);

        if (result.IsSuccess is false)
        {
            if (string.Equals(result.ErrorMessage, ErrorsMessages.EventNotFound, StringComparison.Ordinal))
                return NotFound();

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
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await eventService.DeleteEventAsync(id);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.ErrorMessage, ErrorsMessages.EventNotFound, StringComparison.Ordinal))
                return NotFound();

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