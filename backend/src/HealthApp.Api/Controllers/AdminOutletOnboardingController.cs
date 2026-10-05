using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/admin/outlet-onboarding"), Authorize(Roles = "SuperAdmin")]
public sealed class AdminOutletOnboardingController(IOutletVerificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Pending() => Ok(await service.GetPendingAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        await service.GetAsync(id) is { } result ? Ok(result) : NotFound();

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, DecideOutletVerificationRequest request) =>
        await service.DecideAsync(id, request) is { } result ? Ok(result) : NotFound();
}
