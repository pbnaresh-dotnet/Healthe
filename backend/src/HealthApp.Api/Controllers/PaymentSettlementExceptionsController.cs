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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await exceptions.GetOpenPageAsync(provider, outletId, page, pageSize, cancellationToken);
        return Ok(new { items = result.Items.Select(Map), totalCount = result.TotalCount, page, pageSize,
            totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize) });
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id,
        ResolvePaymentSettlementExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
            return BadRequest(new { message = "Resolution notes are required." });

        var item = await exceptions.GetByIdAsync(id, cancellationToken);
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