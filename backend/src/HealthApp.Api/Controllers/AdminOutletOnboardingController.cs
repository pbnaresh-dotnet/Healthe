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

    [HttpGet("{id:guid}/documents/{documentType}")]
    public async Task<IActionResult> Document(Guid id, string documentType)
    {
        var result = await service.GetDocumentAsync(id, documentType);
        return result is null
            ? NotFound(new { message = "The requested document was not found." })
            : SendProtectedFile(result);
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, DecideOutletVerificationRequest request) =>
        await service.DecideAsync(id, request) is { } result ? Ok(result) : NotFound();
    private IActionResult SendProtectedFile(ProtectedFileDownload result)
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: false);
    }

}
