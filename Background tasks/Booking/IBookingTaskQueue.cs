namespace Events_API.Background_tasks.Booking;

public interface IBookingTaskQueue
{
    void Enqueue(BookingTask task);
    bool TryDequeue(out BookingTask task);
}