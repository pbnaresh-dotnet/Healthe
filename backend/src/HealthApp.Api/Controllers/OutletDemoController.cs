using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlet-demo")]
public sealed class OutletDemoController(IOutletDemoService service) : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> Request(RequestOutletDemoRequest request, CancellationToken cancellationToken)
        => Ok(await service.RequestAsync(request, cancellationToken));
}
