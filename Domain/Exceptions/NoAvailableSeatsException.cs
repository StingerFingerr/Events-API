using Domain.Consts;

namespace Domain.Exceptions;

public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException() : base(ErrorsMessages.NoAvailableSeats)
    {
    }
}
