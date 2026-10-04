using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles="SuperAdmin")]
public sealed class AdminFeaturesController(IServiceCityAdminService cities, ICityAreaAdminService areas) : ControllerBase
{
    [HttpGet("cities")] public async Task<IActionResult> Cities()=>Ok(await cities.GetAsync());
    [HttpPost("cities")] public async Task<IActionResult> CreateCity(CreateServiceCityRequest request)=>Ok(await cities.CreateAsync(request));
    [HttpPut("cities/{id:guid}/enabled")] public async Task<IActionResult> SetCityEnabled(Guid id, [FromQuery]bool enabled)=>((object?)await cities.SetEnabledAsync(id,enabled)) is { } result?Ok(result):NotFound();

    // Legacy city-area endpoints retained for outlet/admin compatibility.
    [HttpGet("city-areas")] public async Task<IActionResult> Areas([FromQuery]string? city)=>Ok(await areas.GetAsync(city));
    [HttpPost("city-areas")] public async Task<IActionResult> CreateArea(CreateCityAreaRequest request)=>Ok(await areas.CreateAsync(request));
}
