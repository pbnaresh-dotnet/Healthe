using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/admin"),Authorize(Roles="SuperAdmin")]

public sealed class AdminController(IAdminService service, IAdminOutletLifecycleService lifecycle):ControllerBase
{
    [HttpGet("errors")] public async Task<IActionResult> Errors([FromQuery] ApplicationErrorQueryRequest request)
        => Ok(await service.GetErrorsAsync(request));

    [HttpGet("errors/{id:guid}")] public async Task<IActionResult> Error(Guid id)
    {
        var item = await service.GetErrorAsync(id);
        return item is null ? NotFound(new { message = "Application error not found." }) : Ok(item);
    }

    [HttpPut("errors/{id:guid}/resolve")] public async Task<IActionResult> ResolveError(Guid id, ResolveApplicationErrorRequest request)
    {
        var item = await service.ResolveErrorAsync(id, request.ResolutionNotes);
        return item is null ? NotFound(new { message = "Application error not found." }) : Ok(item);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> Groups() => Ok(await service.GetOutletGroupsAsync());

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(CreateOutletGroupRequest request) => Ok(await service.CreateOutletGroupAsync(request));

    [HttpPut("groups/{id:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid id, UpdateOutletGroupRequest request)
    {
        try { return Ok(await service.UpdateOutletGroupAsync(id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("outlets/{outletId:guid}/group")]
    public async Task<IActionResult> AssignOutletGroup(Guid outletId, AssignOutletGroupRequest request)
    {
        try { return Ok(await service.AssignOutletGroupAsync(outletId, request.OutletGroupId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("outlets/{outletId:guid}/360")]
    public async Task<IActionResult> Outlet360(Guid outletId)
    {
        var result = await service.GetOutlet360Async(outletId);
        return result is null ? NotFound(new { message = "Outlet not found." }) : Ok(result);
    }

    [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(await service.GetDashboardAsync());
    [HttpGet("outlets")] public async Task<IActionResult> Outlets()=>Ok(await service.GetOutletsAsync());
    [HttpGet("outlets/page")]
    public async Task<IActionResult> OutletsPage([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] string? city, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try { return Ok(await service.GetOutletsPageAsync(search, status, city, page, pageSize)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpGet("users")] public async Task<IActionResult> Users()=>Ok(await service.GetUsersAsync());
    [HttpGet("reports/finance")]
    public async Task<IActionResult> Finance([FromQuery] AdminFinanceReportRequest request)
    {
        try { return Ok(await service.GetFinanceReportAsync(request, HttpContext.RequestAborted)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

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

    [HttpGet("outlets/{outletId:guid}/reactivation-options")]
    public async Task<IActionResult> ReactivationOptions(Guid outletId)
    {
        try { return Ok(await lifecycle.GetReactivationOptionsAsync(outletId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("outlets/{outletId:guid}/reactivate")]
    public async Task<IActionResult> ReactivateOutlet(Guid outletId, AdminOutletReactivationRequest request)
    {
        try { return Ok(await lifecycle.ReactivateAsync(outletId, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
