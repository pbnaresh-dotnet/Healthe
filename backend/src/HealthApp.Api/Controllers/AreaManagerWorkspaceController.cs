using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/area-manager")]
[Authorize(Roles = "AreaManager")]
public sealed class AreaManagerWorkspaceController(HealthAppDbContext db) : ControllerBase
{
    [HttpGet("outlets")]
    public async Task<IActionResult> MyOutlets(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var outlets = await db.AreaManagerOutletAssignments.AsNoTracking()
            .Where(x => x.AreaManagerUserId == userId.Value)
            .Join(db.Outlets, a => a.OutletId, o => o.Id, (a, o) => new
            {
                o.Id, o.Name, o.City, o.State, o.Status, o.Slug,
                o.MobileNumber, o.ContactEmail, a.AssignedAtUtc
            })
            .OrderBy(x => x.City).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(outlets);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var outletIds = db.AreaManagerOutletAssignments.AsNoTracking()
            .Where(x => x.AreaManagerUserId == userId.Value).Select(x => x.OutletId);
        var summary = await db.Outlets.AsNoTracking().Where(x => outletIds.Contains(x.Id))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AssignedOutlets = g.Count(),
                LiveOutlets = g.Count(x => x.Status == OutletStatus.Live),
                PendingOutlets = g.Count(x => x.Status == OutletStatus.Pending),
                SuspendedOutlets = g.Count(x => x.Status == OutletStatus.Suspended)
            })
            .SingleOrDefaultAsync(cancellationToken);
        return Ok(summary ?? new { AssignedOutlets = 0, LiveOutlets = 0, PendingOutlets = 0, SuspendedOutlets = 0 });
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
