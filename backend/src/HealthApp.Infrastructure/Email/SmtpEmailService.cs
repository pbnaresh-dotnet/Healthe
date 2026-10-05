using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure.Email;

public sealed class SmtpEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "HealthApp";
}

public sealed class SmtpEmailService(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        if (!o.Enabled || string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.FromAddress))
            throw new InvalidOperationException("Demo email delivery is not configured. Configure Email SMTP settings before requesting a demo.");

        using var message = new MailMessage
        {
            From = new MailAddress(o.FromAddress, string.IsNullOrWhiteSpace(o.FromName) ? "HealthApp" : o.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(o.Username))
            client.Credentials = new NetworkCredential(o.Username, o.Password);

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }
}
