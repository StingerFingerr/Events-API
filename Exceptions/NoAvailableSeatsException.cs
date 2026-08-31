using Events_API.Consts;

namespace Events_API.Exceptions;

public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException() : base(ErrorsMessages.NoAvailableSeats)
    {
    }
}
