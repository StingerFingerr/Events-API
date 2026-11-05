namespace Domain.Exceptions;

public class ActiveBookingLimitExceededException(int limit)
    : ConflictException($"The active booking limit of {limit} bookings per user has been reached.");
