namespace Users.Domain.Exceptions;

public class ConflictException(string errorMessage) : Exception(errorMessage)
{

}