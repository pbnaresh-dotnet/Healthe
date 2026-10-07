using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/admin/email"), Authorize(Roles="SuperAdmin")]
public sealed class AdminEmailController(ITransactionalEmailService emails) : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromQuery] string to, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { message = "Recipient email address is required." });

        try
        {
            await emails.SendAsync(
                EmailTemplateId.SmtpTest,
                to.Trim(),
                new Dictionary<string, string?>
                {
                    ["To"] = to.Trim(),
                    ["Timestamp"] = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm:ss 'UTC'")
                },
                cancellationToken);

            return Ok(new { sent = true, message = $"Test email sent to {to.Trim()}." });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { sent = false, message = ex.Message });
        }
    }
}
