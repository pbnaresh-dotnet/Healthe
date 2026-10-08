using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin/payment-settlements")]
[Authorize(Roles = "SuperAdmin")]
public sealed class PaymentSettlementController(IPaymentSettlementAccountingService settlements) : ControllerBase
{
    [HttpGet("unreconciled")]
    public async Task<IActionResult> GetUnreconciled([FromQuery] Guid? outletId, CancellationToken cancellationToken)
    {
        var rows = await settlements.GetUnreconciledAsync(outletId, cancellationToken);
        return Ok(rows.Select(Map));
    }

    [HttpPost("reconcile")]
    public async Task<IActionResult> Reconcile(
        RecordPaymentSettlementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await settlements.RecordSettlementAsync(
                new PaymentGatewaySettlementInput(
                    request.PaymentTransactionId,
                    request.ProviderSettlementId,
                    request.GatewayFeeAmount,
                    request.GatewayFeeTaxAmount,
                    request.OtherProviderAdjustmentAmount,
                    request.SettledAtUtc,
                    request.SourceDataJson ?? "",
                    request.ReconciliationReference ?? "",
                    request.ReconciledBy ?? User.Identity?.Name ?? "SuperAdmin"),
                cancellationToken);

            return Ok(Map(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(
        [FromQuery] string provider,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A settlement CSV file is required." });

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only CSV settlement files are supported." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await settlements.ImportCsvAsync(
                string.IsNullOrWhiteSpace(provider) ? "Cashfree" : provider,
                stream,
                User.Identity?.Name ?? "SuperAdmin",
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private static PaymentSettlementReconciliationDto Map(PaymentGatewaySettlement x) =>
        new(
            x.Id,
            x.PaymentTransactionId,
            x.OutletId,
            x.Provider,
            x.ProviderPaymentId,
            x.ProviderSettlementId,
            x.GrossAmount,
            x.GatewayFeeAmount,
            x.GatewayFeeTaxAmount,
            x.OtherProviderAdjustmentAmount,
            x.NetSettlementAmount,
            x.Currency,
            x.Status,
            x.ReconciliationReference,
            x.SettledAtUtc,
            x.ReconciledAtUtc,
            x.ReconciledBy);
}
