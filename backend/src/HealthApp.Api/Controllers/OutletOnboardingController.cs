using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlet-onboarding")]
public sealed class OutletOnboardingController(IOutletOnboardingService service) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> Plans() => Ok(await service.GetPlansAsync());

    [HttpPost("payment")]
    public async Task<IActionResult> Payment(OutletOnboardingPaymentRequest request) => Ok(await service.StartPaymentAsync(request));

    [Authorize(Roles = "OutletAdmin")]
    [HttpGet("me")]
    public async Task<IActionResult> Current()
    {
        var result = await service.GetCurrentAsync();
        return result is null ? NotFound(new { message = "No outlet verification application was found." }) : Ok(result);
    }

    [Authorize(Roles = "OutletAdmin")]
    [HttpPut("me/details")]
    public async Task<IActionResult> CurrentDetails(SaveOutletOnboardingDetailsRequest request)
    {
        var result = await service.SaveCurrentDetailsAsync(request);
        return result is null ? Unauthorized() : Ok(result);
    }

    [Authorize(Roles = "OutletAdmin")]
    [HttpPost("me/documents")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> CurrentDocument([FromForm] string documentType, IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Please select a document." });
        var result = await service.UploadCurrentDocumentAsync(
            documentType, file.OpenReadStream(), file.FileName, file.ContentType, HttpContext.RequestAborted);
        return result is null ? Unauthorized() : Ok(result);
    }

    [Authorize(Roles = "OutletAdmin")]
    [HttpPost("me/submit")]
    public async Task<IActionResult> CurrentSubmit()
    {
        var result = await service.SubmitCurrentAsync();
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var key = Request.Headers["X-Onboarding-Key"].ToString();
        var result = await service.GetAsync(id, key);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPut("{id:guid}/details")]
    public async Task<IActionResult> Details(Guid id, SaveOutletOnboardingDetailsRequest request)
    {
        var key = Request.Headers["X-Onboarding-Key"].ToString();
        var result = await service.SaveDetailsAsync(id, key, request);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Document(Guid id, [FromForm] string documentType, IFormFile file)
    {
        var key = Request.Headers["X-Onboarding-Key"].ToString();
        if (file is null || file.Length == 0) return BadRequest(new { message = "Please select a document." });
        var result = await service.UploadDocumentAsync(
            id, key, documentType, file.OpenReadStream(), file.FileName, file.ContentType, HttpContext.RequestAborted);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var key = Request.Headers["X-Onboarding-Key"].ToString();
        var result = await service.SubmitAsync(id, key);
        return result is null ? Unauthorized() : Ok(result);
    }
}
