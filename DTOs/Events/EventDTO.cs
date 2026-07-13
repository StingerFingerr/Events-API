using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events;

public class EventDto
{
    [Required]
    public int Id { get; set; }
    [Required]
    public string Title { get; set; }
    public string Description { get; set; }
    [Required]
    public DateTime StartAt { get; set; }
    [Required]
    public DateTime EndAt { get; set; }
    
    
}