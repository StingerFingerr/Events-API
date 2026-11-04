namespace Domain.Exceptions;

public class ActiveBookingLimitExceededException() : ConflictException("The active booking limit has been reached.");
