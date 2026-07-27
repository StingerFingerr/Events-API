namespace Events_API.DTOs;

public record Result<T>(bool IsSuccess, T? Value, string? ErrorMessage)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}

public record Result(bool IsSuccess, string? ErrorMessage)
{
    public static Result Success() => new(true, null);
    public static Result Failure(string errorMessage) => new(false, errorMessage);
}
