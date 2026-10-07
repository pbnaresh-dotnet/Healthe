using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlets/me/staff"), Authorize(Roles = "OutletAdmin")]
public sealed class OutletStaffController(IOutletStaffService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await service.GetAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreateOutletStaffRequest request) => Ok(await service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOutletStaffRequest request)
        => Ok(await service.UpdateAsync(id, request));
}
