using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;

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
    public string FromName { get; set; } = "Broccoly";
    public string ReplyToAddress { get; set; } = "";
    public string AdminNotificationAddress { get; set; } = "";
    public string SupportAddress { get; set; } = "support@broccoly.in";
    public string OutletAdminUrl { get; set; } = "https://outlet.broccoly.in";
    public int TimeoutSeconds { get; set; } = 20;
}

public sealed class SmtpEmailService(IOptions<SmtpEmailOptions> options) : IEmailService
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        SendAsync(
            new EmailMessage(
                to,
                subject,
                body,
                $"<!doctype html><html><body style=\"font-family:Arial,sans-serif;line-height:1.6;color:#173b24\"><div style=\"max-width:640px;margin:20px auto;padding:24px\"><pre style=\"font-family:Arial,sans-serif;white-space:pre-wrap\">{WebUtility.HtmlEncode(body)}</pre></div></body></html>"),
            cancellationToken);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        if (!o.Enabled)
            throw new InvalidOperationException("Email delivery is disabled. Set Email:Enabled=true after configuring SMTP.");

        if (string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.FromAddress))
            throw new InvalidOperationException("Email SMTP is not configured. Configure Email:Host and Email:FromAddress.");

        if (!MailAddress.TryCreate(message.To, out _))
            throw new ArgumentException("Recipient email address is invalid.", nameof(message));

        using var mail = new MailMessage
        {
            From = new MailAddress(
                o.FromAddress,
                string.IsNullOrWhiteSpace(o.FromName) ? "Broccoly" : o.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        mail.To.Add(message.To);

        var replyTo = message.ReplyTo ?? o.ReplyToAddress;
        if (!string.IsNullOrWhiteSpace(replyTo) && MailAddress.TryCreate(replyTo, out _))
            mail.ReplyToList.Add(replyTo);

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            var textView = AlternateView.CreateAlternateViewFromString(
                message.TextBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Plain);
            var htmlView = AlternateView.CreateAlternateViewFromString(
                message.HtmlBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Html);
            mail.AlternateViews.Add(textView);
            mail.AlternateViews.Add(htmlView);
        }

        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Clamp(o.TimeoutSeconds, 5, 120) * 1000
        };

        if (string.IsNullOrWhiteSpace(o.Username))
            throw new InvalidOperationException("Email SMTP username is not configured.");

        client.Credentials = new NetworkCredential(o.Username.Trim(), o.Password ?? string.Empty);

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail);
    }
}
