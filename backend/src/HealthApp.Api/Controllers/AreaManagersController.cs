using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin/area-managers")]
[Authorize(Roles = "SuperAdmin")]
public sealed class AreaManagersController(HealthAppDbContext db, IPasswordService passwords) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var managers = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.AreaManager)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new
            {
                x.Id, x.Email, x.FirstName, x.LastName, x.MobileNumber, x.IsActive,
                OutletIds = db.AreaManagerOutletAssignments.Where(a => a.AreaManagerUserId == x.Id).Select(a => a.OutletId).ToList(),
                OutletNames = db.AreaManagerOutletAssignments.Where(a => a.AreaManagerUserId == x.Id)
                    .Join(db.Outlets, a => a.OutletId, o => o.Id, (a, o) => o.Name).ToList()
            })
            .ToListAsync(cancellationToken);
        return Ok(managers);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAreaManagerRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest(new { message = "Provide an email and a password with at least 12 characters." });
        if (await db.Users.AnyAsync(x => x.Email == email && x.OutletId == null, cancellationToken))
            return Conflict(new { message = "A platform user with this email already exists." });

        var outletIds = (request.OutletIds ?? []).Distinct().ToArray();
        var existingOutletIds = await db.Outlets.Where(x => outletIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (existingOutletIds.Count != outletIds.Length)
            return BadRequest(new { message = "One or more assigned outlets do not exist." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            MobileNumber = request.MobileNumber?.Trim(),
            Role = UserRole.AreaManager,
            IsActive = true,
            OutletId = null
        };
        db.Users.Add(user);
        foreach (var outletId in outletIds)
        {
            db.AreaManagerOutletAssignments.Add(new AreaManagerOutletAssignment
            {
                Id = Guid.NewGuid(),
                AreaManagerUserId = user.Id,
                OutletId = outletId,
                AssignedAtUtc = DateTime.UtcNow,
                AssignedByUserId = GetCurrentUserId()
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { id = user.Id }, new { user.Id, user.Email, user.FirstName, user.LastName, user.MobileNumber, user.Role, AssignedOutletCount = outletIds.Length });
    }

    [HttpPut("{userId:guid}/outlets")]
    public async Task<IActionResult> AssignOutlets(Guid userId, AssignAreaManagerOutletsRequest request, CancellationToken cancellationToken)
    {
        var manager = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.AreaManager, cancellationToken);
        if (manager is null) return NotFound(new { message = "Area Manager not found." });

        var outletIds = (request.OutletIds ?? []).Distinct().ToArray();
        var existingOutletIds = await db.Outlets.Where(x => outletIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (existingOutletIds.Count != outletIds.Length)
            return BadRequest(new { message = "One or more assigned outlets do not exist." });

        var current = await db.AreaManagerOutletAssignments.Where(x => x.AreaManagerUserId == userId).ToListAsync(cancellationToken);
        db.AreaManagerOutletAssignments.RemoveRange(current);
        foreach (var outletId in outletIds)
        {
            db.AreaManagerOutletAssignments.Add(new AreaManagerOutletAssignment
            {
                Id = Guid.NewGuid(),
                AreaManagerUserId = userId,
                OutletId = outletId,
                AssignedAtUtc = DateTime.UtcNow,
                AssignedByUserId = GetCurrentUserId()
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { manager.Id, AssignedOutletIds = outletIds, AssignedOutletCount = outletIds.Length });
    }

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid userId, SetAreaManagerStatusRequest request, CancellationToken cancellationToken)
    {
        var manager = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.AreaManager, cancellationToken);
        if (manager is null) return NotFound(new { message = "Area Manager not found." });
        manager.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { manager.Id, manager.IsActive });
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public sealed record CreateAreaManagerRequest(
    string Email, string Password, string FirstName, string LastName,
    string? MobileNumber, List<Guid>? OutletIds);

public sealed record AssignAreaManagerOutletsRequest(List<Guid>? OutletIds);
public sealed record SetAreaManagerStatusRequest(bool IsActive);
