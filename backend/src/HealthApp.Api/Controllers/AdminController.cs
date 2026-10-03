using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/admin"),Authorize(Roles="SuperAdmin")]

public sealed class AdminController(IAdminService service):ControllerBase
{
    [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(await service.GetDashboardAsync());
    [HttpGet("outlets")] public async Task<IActionResult> Outlets()=>Ok(await service.GetOutletsAsync());
    [HttpGet("users")] public async Task<IActionResult> Users()=>Ok(await service.GetUsersAsync());
    [HttpGet("reports/revenue")] public async Task<IActionResult> Revenue()=>Ok(await service.GetRevenueAsync());
}
