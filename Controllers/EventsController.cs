using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;
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
        var eventFound = eventService.GetEventById(id);
        return Ok(eventFound);
    }

    [HttpGet]
    [ProducesResponseType<List<EventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetEvents([FromQuery] GetEventsByFiltersDto filters)
    {
        var eventsByFilters = eventService.GetEventsByFilters(filters);
        return Ok(eventsByFilters);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult PostEvent([FromBody] CreateEventDto eventData)
    {
        var createdEvent = eventService.CreateEvent(eventData);
        return CreatedAtAction(
            nameof(GetEvent),
            new { id = createdEvent.Id },
            createdEvent);
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult PatchEvent(int id, [FromBody] UpdateTitleDto dto)
    {
        var updatedEvent = eventService.UpdateEvent(id, dto.Title);
        return Ok(updatedEvent);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult PutEvent(int id, CreateEventDto newEventData)
    {
        var updatedEvent = eventService.UpdateEvent(id, newEventData);
        return Ok(updatedEvent);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult DeleteEvent(int id)
    {
        eventService.DeleteEvent(id);
        return NoContent();
    }
}