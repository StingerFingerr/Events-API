using System.ComponentModel.DataAnnotations;
using Application.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Events_API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserService users) : ControllerBase
{
    public sealed record Credentials([Required] string Login, [Required] string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(Credentials credentials)
    {
        var id = await users.RegisterAsync(credentials.Login, credentials.Password);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials credentials)
    {
        var token = await users.LoginAsync(credentials.Login, credentials.Password);
        return Ok(new { token });
    }
}
