using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/auth")] public sealed class AuthController(IAuthService auth):ControllerBase {
    [HttpPost("login"), EnableRateLimiting("auth")] public async Task<ActionResult<AuthResponse>> Login(LoginRequest r) {
        var x=await auth.LoginAsync(r);
        return x is null?Unauthorized(new {
            message="Invalid email or password."
        }):Ok(x);
    }
    [HttpPost("register"), EnableRateLimiting("auth")] public async Task<ActionResult<AuthResponse>> Register(RegisterRequest r)=>Ok(await auth.RegisterAsync(r));
    [Authorize,HttpGet("me")] public ActionResult Me()=>Ok(new {
        authenticated=true,userId=User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,role=User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
    });
    [Authorize,HttpPost("logout")] public IActionResult Logout()=>NoContent();
}
