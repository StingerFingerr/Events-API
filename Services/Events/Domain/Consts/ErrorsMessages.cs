namespace Events.Domain.Consts;

public abstract record ErrorsMessages
{
    public const string EventAlreadyExists = "Event with the same name already exists";
    public const string EventNotFound = "Event not found";
    public const string CannotCreateEventInThePast = "Cannot create an event in the past.";
    public const string CannotCreateEventWithStartLaterThenEnd = "Сannot create an event with a start date later than the end date..";
    public const string EventTitleIsShort = "Event title must contain at least 5 characters and cannot be blank.";
    public const string EventTotalSeatsMustBePositive = "Total number of seats must be greater than zero.";
    public const string EventCapacityCannotBeLessThanReservedSeats =
        "Total number of seats cannot be less than the number of reserved seats.";
    public const string InternalServerError = "An internal server error occurred";
}
