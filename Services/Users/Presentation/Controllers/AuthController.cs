using System.ComponentModel.DataAnnotations;
using Users.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Users.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
[Route("api/auth")]
public class AuthController(IUserService users) : ControllerBase
{
    public sealed record Credentials([Required] string Login, [Required] string Password);

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(Credentials credentials)
    {
        await users.RegisterAsync(credentials.Login, credentials.Password);
        return NoContent();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials credentials)
    {
        var token = await users.LoginAsync(credentials.Login, credentials.Password);
        return Ok(new { token });
    }
}
