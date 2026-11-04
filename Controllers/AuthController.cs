using System.ComponentModel.DataAnnotations;
using Application.Services.Auth;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
[Route("api/auth")]
public class AuthController(UserService users) : ControllerBase
{
    public sealed record Credentials([Required] string Login, [Required] string Password);

    public sealed record Registration(
        [Required] string Login,
        [Required] string Password,
        [EnumDataType(typeof(UserRole))]
        [property: JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
        UserRole Role = UserRole.User);

    [HttpPost("register")]
    public async Task<IActionResult> Register(Registration credentials)
    {
        var id = await users.RegisterAsync(credentials.Login, credentials.Password, credentials.Role);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials credentials)
    {
        var token = await users.LoginAsync(credentials.Login, credentials.Password);
        return Ok(new { token });
    }
}
