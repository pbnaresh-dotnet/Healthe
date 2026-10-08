using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/outlets/me/trial")]
[Authorize(Roles="OutletAdmin")]
public sealed class TrialController(ITrialService service):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()=>Ok(await service.GetCurrentAsync());

    [HttpPost]
    public async Task<IActionResult> Start(StartTrialRequest request)
    {
        try{return Ok(await service.StartAsync(request));}
        catch(KeyNotFoundException ex){return NotFound(new{message=ex.Message});}
        catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}
        catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] string? reason=null)
    {
        try{return Ok(await service.CancelAsync(reason??"Cancelled by outlet administrator"));}
        catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}
    }
}