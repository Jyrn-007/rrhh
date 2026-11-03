using System.Security.Claims;
using KitsuneHrm.Api.Models;
using KitsuneHrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KitsuneHrm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    string? Ua => Request.Headers.UserAgent.ToString();
    int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    IActionResult Respond(AuthResult r) =>
        r.Ok ? Ok(r.Data) : StatusCode(r.Status, new { error = r.Error, codigo = r.Codigo });

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest req) => Respond(await auth.LoginAsync(req, Ip, Ua));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req) => Respond(await auth.RefreshAsync(req.RefreshToken, Ip, Ua));

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req)
    {
        await auth.LogoutAsync(req?.RefreshToken, UserId, User.Identity!.Name!, Ip, Ua);
        return NoContent();
    }

    [HttpPost("cambiar-password"), Authorize]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordRequest req) =>
        Respond(await auth.CambiarPasswordAsync(UserId, req, Ip, Ua));

    [HttpGet("me"), Authorize]
    public IActionResult Me() => Ok(new
    {
        id = UserId,
        username = User.Identity!.Name,
        rol = User.FindFirstValue(ClaimTypes.Role),
        permisos = User.FindAll("permiso").Select(c => c.Value),
        debeCambiarPassword = User.FindFirstValue("must_change") == "true",
    });
}
