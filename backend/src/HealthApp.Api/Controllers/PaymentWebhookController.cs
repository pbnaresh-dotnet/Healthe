using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/payments")]
public sealed class PaymentWebhookController(IPaymentService payments) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("cashfree/webhook")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> CashfreeWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers["x-webhook-signature"].ToString();
        var timestamp = Request.Headers["x-webhook-timestamp"].ToString();
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp))
            return Unauthorized(new { message = "payment gateway webhook signature headers are required." });

        var result = await payments.HandleWebhookAsync(
            rawBody,
            signature,
            timestamp,
            cancellationToken);

        return Ok(result);
    }
}
