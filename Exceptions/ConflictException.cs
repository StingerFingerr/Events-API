namespace Events_API.Exceptions;

public class ConflictException(string errorMessage) : Exception(errorMessage)
{
    
}