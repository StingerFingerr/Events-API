namespace Bookings.Domain.Exceptions;

public class ConflictException(string errorMessage) : Exception(errorMessage)
{

}