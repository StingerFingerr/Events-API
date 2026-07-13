using Events_API.DTOs.Events;
using Events_API.DTOs.Results;
using Events_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ApiBaseResult> GetEvent(int id)
    {
        return await eventService.GetEventByIdAsync(id);
    }
    
    [HttpPost]
    public async Task<ApiBaseResult> PostEvent([FromBody] CreateEventDto eventData)
    {
        return await eventService.CreateEventAsync(eventData);
    }
    
    [HttpPatch("{id:int}/{newTitle}")]
    public async Task<ApiBaseResult> PatchEvent(int id, string newTitle)
    {
        return await eventService.UpdateEventAsync(id, newTitle);
    }

    [HttpPut]
    public async Task<ApiBaseResult> PutEvent(EventUpdateDto newEventData)
    {
        return await eventService.UpdateEventAsync(newEventData);
    }

     
}