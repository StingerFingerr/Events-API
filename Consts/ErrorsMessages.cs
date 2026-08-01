namespace Events_API.Consts;

public abstract record ErrorsMessages
{
    public const string EventAlreadyExists = "Event with the same name already exists";
    public const string EventNotFound = "Event with not found";
    public const string CannotUpdateEventTitle = "Cannot update event title";
    public const string CannotDeleteEvent = "Cannot delete event";
    public const string CannotCreateEventInThePast = "Cannot create an event in the past.";
    public const string CannotCreateEventEndsAfterStarts = "Cannot create an event with a start date that ends after.";
    public const string EventTitleIsShort = "Cannot create an event with a short title (less than 3 characters)";
}