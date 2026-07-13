using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events;

public class CreateEventDto
{
    [Required] public string Title { get; set; }
    [Required] public DateTime StartAt { get; set; }
    [Required] public DateTime EndAt { get; set; }
}