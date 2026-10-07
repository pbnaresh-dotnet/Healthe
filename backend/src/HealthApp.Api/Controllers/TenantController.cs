using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/tenant")]
public sealed class TenantController(
    ITenantHostResolver resolver,
    ITenantContext tenant,
    IMarketplaceService marketplace) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string? host)
    {
        var outlet = await resolver.ResolveAsync(string.IsNullOrWhiteSpace(host)
            ? HttpContext.Request.Host.Host
            : host);

        if (outlet is null)
            return NotFound(new { message = "No active outlet tenant is mapped to this hostname." });

        // Establish the resolved tenant for the remainder of this request so the
        // normal marketplace visibility and subscription checks are reused.
        tenant.Set(outlet.Id, outlet.Slug);

        var dto = await marketplace.GetOutletAsync(outlet.Slug);
        return dto is null
            ? NotFound(new { message = "The outlet is not currently available to customers." })
            : Ok(dto);
    }
}
