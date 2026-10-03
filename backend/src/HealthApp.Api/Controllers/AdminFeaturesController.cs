using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles="SuperAdmin")]
public sealed class AdminFeaturesController(ICityAreaAdminService areas) : ControllerBase
{
    [HttpGet("city-areas")] public async Task<IActionResult> Areas([FromQuery]string? city)=>Ok(await areas.GetAsync(city));
    [HttpPost("city-areas")] public async Task<IActionResult> CreateArea(CreateCityAreaRequest request)=>Ok(await areas.CreateAsync(request));
}
