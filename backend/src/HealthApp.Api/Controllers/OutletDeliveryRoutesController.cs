using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlets/me/delivery-routes"), Authorize(Roles = "OutletAdmin,OutletManager")]
public sealed class OutletDeliveryRoutesController(IDeliveryRouteService service) : ControllerBase
{
    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers() => Ok(await service.GetDriversAsync());

    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(CreateDriverRequest request)
        => Ok(await service.CreateDriverAsync(request));

    [HttpGet]
    public async Task<IActionResult> Plan([FromQuery] DateTime? date, [FromQuery] int? mealSlot)
        => Ok(await service.GetPlanAsync((date ?? DateTime.UtcNow).Date, mealSlot ?? 2));

    [HttpPost("plan")]
    public async Task<IActionResult> PlanRoutes(PlanDeliveryRoutesRequest request)
        => Ok(await service.PlanRoutesAsync(request));

    [HttpGet("my")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyRoute([FromQuery] DateTime? date, [FromQuery] int? mealSlot)
        => Ok(await service.GetDriverPlanAsync((date ?? DateTime.UtcNow).Date, mealSlot ?? 2));

    [HttpPost("{routeId:guid}/start")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> Start(Guid routeId)
        => Ok(await service.StartDriverRouteAsync(routeId));

    [HttpPost("stops/{stopId:guid}/complete")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> CompleteStop(Guid stopId)
        => Ok(await service.CompleteDriverStopAsync(stopId));

    [HttpPost("{routeId:guid}/dispatch")]
    public async Task<IActionResult> Dispatch(Guid routeId)
        => Ok(await service.DispatchRouteAsync(routeId));
}
