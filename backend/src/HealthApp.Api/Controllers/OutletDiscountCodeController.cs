using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me/discount-codes"),Authorize(Roles="OutletAdmin")]

public sealed class OutletDiscountCodeController(IOutletDiscountCodeService service):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await service.GetAsync());
    [HttpPost] public async Task<IActionResult> Create(CreateDiscountCodeRequest request)=>Ok(await service.CreateAsync(request));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Disable(Guid id)=>await service.DisableAsync(id)?NoContent():NotFound();
}
