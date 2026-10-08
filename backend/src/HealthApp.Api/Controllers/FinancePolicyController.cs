using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin/finance-policy")]
[Authorize(Roles = "SuperAdmin")]
public sealed class FinancePolicyController(IFinancePolicyService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string code = "FINANCE-CALCULATION-POLICY", [FromQuery] DateTime? asOfUtc = null)
    {
        var result = await service.GetAsync(code, asOfUtc);
        return result is null
            ? NotFound(new { message = "Finance policy document not found." })
            : Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string code = "FINANCE-CALCULATION-POLICY")
        => Ok(await service.GetHistoryAsync(code));
}
