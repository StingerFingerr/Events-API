using System.Net;

namespace Events_API.DTOs.Results;

public class ApiResult<T> : ApiBaseResult
{
    public required T Data { get; set; }

    public static ApiBaseResult Ok(T data) =>
        new ApiResult<T>()
        {
            Success = true,
            Message = string.Empty,
            StatusCode = HttpStatusCode.OK,
            Data = data
        };

    public static ApiBaseResult Created(T data) =>
        new ApiResult<T>()
        {
            Success = true,
            Message = string.Empty,
            StatusCode = HttpStatusCode.Created,
            Data = data
        };
}

public class ApiResult : ApiBaseResult
{
    public static ApiResult Ok(string message = "") =>
        new()
        {
            Success = true,
            StatusCode = HttpStatusCode.OK,
            Message = message
        };
    
    public static ApiResult Failed(string message) =>
        new()
        {
            Success = false,
            StatusCode = HttpStatusCode.BadRequest,
            Message = message
        };
}