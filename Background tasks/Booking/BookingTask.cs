namespace Events_API.Background_tasks.Booking;

public class BookingTask
{
    public Guid BookingId { get; init; }
    public Guid EventId { get; init; }
}
