using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin/payment-settlements/exceptions")]
[Authorize(Roles = "SuperAdmin")]
public sealed class PaymentSettlementExceptionsController(
    IPaymentSettlementReconciliationExceptionRepository exceptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOpen(
        [FromQuery] string? provider,
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        var rows = await exceptions.GetOpenAsync(provider, outletId);
        return Ok(rows.Select(Map));
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id,
        ResolvePaymentSettlementExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
            return BadRequest(new { message = "Resolution notes are required." });

        var rows = await exceptions.GetOpenAsync();
        var item = rows.FirstOrDefault(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Open reconciliation exception was not found." });

        item.Status = "Resolved";
        item.ResolutionNotes = request.ResolutionNotes.Trim();
        item.ResolvedBy = request.ResolvedBy?.Trim() ?? User.Identity?.Name ?? "SuperAdmin";
        item.ResolvedAtUtc = DateTime.UtcNow;
        await exceptions.UpdateAsync(item);

        return Ok(Map(item));
    }

    private static PaymentSettlementReconciliationExceptionDto Map(PaymentSettlementReconciliationException x) =>
        new(
            x.Id,
            x.Provider,
            x.ProviderPaymentId,
            x.ProviderSettlementId,
            x.ExceptionType,
            x.Status,
            x.ReportedGrossAmount,
            x.ReportedNetSettlementAmount,
            x.Currency,
            x.ErrorMessage,
            x.AssignedTo,
            x.ResolutionNotes,
            x.CreatedAtUtc,
            x.ResolvedAtUtc,
            x.ResolvedBy);
}