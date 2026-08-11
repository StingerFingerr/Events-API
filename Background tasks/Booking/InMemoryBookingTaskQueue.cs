using System.Collections.Concurrent;

namespace Events_API.Background_tasks.Booking;

public class InMemoryBookingTaskQueue : IBookingTaskQueue
{
    private readonly ConcurrentQueue<BookingTask> _queue = new();

    public void Enqueue(BookingTask task)
    {
        _queue.Enqueue(task);
    }

    public bool TryDequeue(out BookingTask task)
    {
        return _queue.TryDequeue(out task!);
    }
}
