namespace Domain.Exceptions;

public class PastEventBookingException() : ConflictException("This event has already started.");
