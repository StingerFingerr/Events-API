using System.Net;

namespace Events_API.DTOs.Results;

public class ApiBaseResult
{
    public required bool Success { get; set; }
    public required HttpStatusCode StatusCode { get; set; }
    public required string Message { get; set; }
}