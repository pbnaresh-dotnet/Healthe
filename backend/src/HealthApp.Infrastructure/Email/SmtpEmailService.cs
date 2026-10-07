using System.Net;
using System.Text;
using HealthApp.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Email;

public sealed class SmtpEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 465;
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
            throw new InvalidOperationException(
                "Email delivery is disabled. Set Email:Enabled=true after configuring SMTP.");

        if (string.IsNullOrWhiteSpace(o.Host) ||
            string.IsNullOrWhiteSpace(o.FromAddress))
            throw new InvalidOperationException(
                "Email SMTP is not configured. Configure Email:Host and Email:FromAddress.");

        if (!MailAddress.TryCreate(message.To, out _))
            throw new ArgumentException(
                "Recipient email address is invalid.",
                nameof(message));

        if (string.IsNullOrWhiteSpace(o.Username))
            throw new InvalidOperationException(
                "Email SMTP username is not configured.");

        if (string.IsNullOrWhiteSpace(o.Password))
            throw new InvalidOperationException(
                "Email SMTP password is not configured.");

        if (!MailAddress.TryCreate(o.FromAddress, out _))
            throw new InvalidOperationException(
                "Email SMTP FromAddress is invalid.");

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(o.FromName) ? "Broccoly" : o.FromName,
            o.FromAddress.Trim()));
        email.To.Add(MailboxAddress.Parse(message.To.Trim()));
        email.Subject = message.Subject ?? "";

        var replyTo = message.ReplyTo ?? o.ReplyToAddress;
        if (!string.IsNullOrWhiteSpace(replyTo) && MailAddress.TryCreate(replyTo, out _))
            email.ReplyTo.Add(MailboxAddress.Parse(replyTo.Trim()));

        var bodyBuilder = new BodyBuilder
        {
            TextBody = message.TextBody ?? "",
            HtmlBody = message.HtmlBody ?? ""
        };
        email.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = Math.Clamp(o.TimeoutSeconds, 5, 120) * 1000;

        var secureSocketOptions = o.Port == 465 && o.EnableSsl
            ? SecureSocketOptions.SslOnConnect
            : o.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await client.ConnectAsync(
                o.Host.Trim(),
                o.Port,
                secureSocketOptions,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await client.AuthenticateAsync(
                o.Username.Trim(),
                o.Password,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await client.SendAsync(email, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None);
                }
                catch
                {
                    // The message has already been submitted; don't mask the send result.
                }
            }
        }
    }
}
