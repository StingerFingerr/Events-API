namespace Events_API.Exceptions;

public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException() : base("No seats are available for this event.")
    {
    }
}
