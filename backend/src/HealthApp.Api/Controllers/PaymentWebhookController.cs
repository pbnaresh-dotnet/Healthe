using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/payments")]
public sealed class PaymentWebhookController(IPaymentService payments) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("webhook")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> PaymentGatewayWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var headers = Request.Headers
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await payments.HandleWebhookAsync(rawBody, headers, cancellationToken);
        if (string.Equals(result.Status, "AlreadyProcessing", StringComparison.Ordinal))
        {
            // Ask the provider to retry rather than acknowledging delivery while the active
            // worker still owns the lease; this also covers an active worker that later crashes.
            Response.Headers.RetryAfter = "5";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Ok(result);
    }
}
