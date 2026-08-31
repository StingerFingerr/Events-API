namespace Events_API.Consts;

public abstract record ErrorsMessages
{
    public const string EventAlreadyExists = "Event with the same name already exists";
    public const string EventNotFound = "Event with not found";
    public const string CannotUpdateEventTitle = "Cannot update event title";
    public const string CannotDeleteEvent = "Cannot delete event";
    public const string CannotCreateEventInThePast = "Cannot create an event in the past.";
    public const string CannotCreateEventWithStartLaterThenEnd = "Сannot create an event with a start date later than the end date..";
    public const string EventTitleIsShort = "Cannot create an event with a short title (less than 3 characters)";
    public const string EventTotalSeatsMustBePositive = "Total number of seats must be greater than zero.";
    public const string EventCapacityCannotBeLessThanReservedSeats =
        "Total number of seats cannot be less than the number of reserved seats.";
    public const string NoAvailableSeats = "No available seats for this event";
    public const string InternalServerError = "Some internal server error occured";
}
