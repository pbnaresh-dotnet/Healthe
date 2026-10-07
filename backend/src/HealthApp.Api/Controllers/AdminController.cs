using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
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
    [HttpGet("domains")] public async Task<IActionResult> Domains()=>Ok(await service.GetOutletDomainsAsync());
    [HttpPut("domains/{id:guid}/status")] public async Task<IActionResult> SetDomainStatus(Guid id, SetOutletDomainStatusRequest request)
    {
        if (!Enum.TryParse<HealthApp.Domain.Enums.OutletDomainStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Unsupported domain status. Use Pending, Verified, Active or Disabled." });

        try { return Ok(await service.SetOutletDomainStatusAsync(id, status)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
