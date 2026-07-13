using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events;

public class EventUpdateDto : CreateEventDto
{
    [Required] public int Id { get; set; }  
}