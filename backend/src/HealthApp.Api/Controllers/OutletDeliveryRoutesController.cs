using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlets/me/delivery-routes"), Authorize(Roles = "OutletAdmin")]
public sealed class OutletDeliveryRoutesController(IDeliveryRouteService service) : ControllerBase
{
    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers() => Ok(await service.GetDriversAsync());

    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(CreateDriverRequest request)
        => Ok(await service.CreateDriverAsync(request));

    [HttpGet]
    public async Task<IActionResult> Plan([FromQuery] DateTime? date)
        => Ok(await service.GetPlanAsync((date ?? DateTime.UtcNow).Date));

    [HttpPost("plan")]
    public async Task<IActionResult> PlanRoutes(PlanDeliveryRoutesRequest request)
        => Ok(await service.PlanRoutesAsync(request));
}
