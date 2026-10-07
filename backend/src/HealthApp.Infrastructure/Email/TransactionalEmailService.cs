using System.Net.Mail;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Email;

public sealed class TransactionalEmailService(
    IEmailService mail,
    IOptions<SmtpEmailOptions> options,
    ILogger<TransactionalEmailService> logger) : ITransactionalEmailService
{
    public Task SendAsync(
        EmailTemplateId template,
        string to,
        IReadOnlyDictionary<string, string?> data,
        CancellationToken cancellationToken = default)
    {
        var rendered = EmailTemplateRenderer.Render(template, data, options.Value);
        return mail.SendAsync(
            new EmailMessage(to, rendered.Subject, rendered.TextBody, rendered.HtmlBody, rendered.ReplyTo),
            cancellationToken);
    }

    public async Task<bool> TrySendAsync(
        EmailTemplateId template,
        string to,
        IReadOnlyDictionary<string, string?> data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(template, to, data, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Transactional email failed. Template={Template}, Recipient={Recipient}", template, to);
            return false;
        }
    }
}

internal sealed record RenderedEmail(string Subject, string TextBody, string HtmlBody, string? ReplyTo);

internal static class EmailTemplateRenderer
{
    private const string Version = "v1";

    public static RenderedEmail Render(
        EmailTemplateId template,
        IReadOnlyDictionary<string, string?> data,
        SmtpEmailOptions options)
    {
        string V(string key) => data.TryGetValue(key, out var value) ? value ?? "" : "";
        string H(string key) => System.Net.WebUtility.HtmlEncode(V(key));
        var support = string.IsNullOrWhiteSpace(options.SupportAddress) ? "support@broccoly.in" : options.SupportAddress.Trim();
        var supportHtml = System.Net.WebUtility.HtmlEncode(support);
        var footerText = $"Broccoly | Healthy food business SaaS | Support: {support}\nTemplate {Version}";
        var footerHtml = $"<p style=\"margin:28px 0 0;color:#6b7b71;font-size:12px;line-height:1.5\">Broccoly · Healthy food business SaaS<br/>Support: {supportHtml}<br/><span style=\"color:#9aa69f\">Template {Version}</span></p>";

        return template switch
        {
            EmailTemplateId.SmtpTest => Build(
                "Broccoly SMTP test email",
                $"Your Broccoly SMTP configuration is working.\n\nSent to: {V("To")}\nTime (UTC): {V("Timestamp")}\n\n{footerText}",
                $"<h1>SMTP is working ✓</h1><p>Your Broccoly SMTP configuration is working correctly.</p><table><tr><td><b>Recipient</b></td><td>{H("To")}</td></tr><tr><td><b>Time (UTC)</b></td><td>{H("Timestamp")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.OutletDemoAccess => Build(
                "Your 7-day Broccoly demo is ready",
                $"Hello {V("FirstName")},\n\nYour Broccoly demo account is ready.\n\nLogin: {V("Email")}\nPassword: {V("Password")}\nDemo portal: {V("PortalUrl")}\nValid until: {V("ExpiresAtUtc")}\n\nYou can explore customers, subscriptions, meal packages, recipes, kitchen orders, drivers and delivery planning.\n\nThis demo expires automatically after 7 days.\n\n{footerText}",
                $"<h1>Your 7-day demo is ready</h1><p>Hello {H("FirstName")}, your Broccoly demo workspace is ready.</p><table><tr><td><b>Login</b></td><td>{H("Email")}</td></tr><tr><td><b>Password</b></td><td>{H("Password")}</td></tr><tr><td><b>Demo portal</b></td><td><a href=\"{H("PortalUrl")}\" style=\"color:#1d6b3a\">{H("PortalUrl")}</a></td></tr><tr><td><b>Valid until</b></td><td>{H("ExpiresAtUtc")}</td></tr></table><p>You can explore customers, subscriptions, meal packages, recipes, kitchen orders, drivers and delivery planning.</p><div style=\"background:#eef7f0;border-radius:12px;padding:14px\">Demo access expires automatically after 7 days.</div>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.CustomerWelcome => Build(
                $"Welcome to {V("OutletName")}",
                $"Hello {V("FirstName")},\n\nWelcome to {V("OutletName")}. Your customer account is ready.\n\nOpen your meal storefront: {V("StorefrontUrl")}\n\nYou can manage your profile, delivery addresses, allergies, meal plans, packages and subscriptions from your account.\n\n{footerText}",
                $"<h1>Welcome to {H("OutletName")}</h1><p>Hello {H("FirstName")}, your customer account is ready.</p><p><a href=\"{H("StorefrontUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Open your meal storefront →</a></p><p>You can manage your profile, delivery addresses, allergies, meal plans, packages and subscriptions from your account.</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.OutletOnboardingPaymentConfirmed => Build(
                "Broccoly setup payment received",
                $"Hello {V("OwnerName")},\n\nWe have received the Broccoly setup payment for {V("OutletName")}.\n\nPlan: {V("PlanName")}\nBilling cycle: {V("BillingCycle")}\nSetup fee: {V("SetupFee")}\nPayment reference: {V("PaymentReference")}\n\nYour outlet account is now ready for verification. Next, upload the required documents and submit the application for review.\n\nOutlet admin portal: {V("OutletAdminUrl")}\n\n{footerText}",
                $"<h1>Setup payment received ✓</h1><p>Hello {H("OwnerName")}, we have received the Broccoly setup payment for <b>{H("OutletName")}</b>.</p><table><tr><td><b>Plan</b></td><td>{H("PlanName")}</td></tr><tr><td><b>Billing cycle</b></td><td>{H("BillingCycle")}</td></tr><tr><td><b>Setup fee</b></td><td>{H("SetupFee")}</td></tr><tr><td><b>Payment reference</b></td><td>{H("PaymentReference")}</td></tr></table><p>Your outlet account is now ready for verification. Upload the required documents and submit the application for review.</p><p><a href=\"{H("OutletAdminUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Open outlet admin →</a></p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.OutletVerificationSubmitted => Build(
                $"Outlet verification submitted: {V("OutletName")}",
                $"A Broccoly outlet verification application has been submitted.\n\nOutlet: {V("OutletName")}\nOwner: {V("OwnerName")}\nEmail: {V("Email")}\nPlan: {V("PlanName")}\nSubmitted: {V("SubmittedAtUtc")}\n\nReview it in the outlet administration workflow.\n\n{footerText}",
                $"<h1>Verification application submitted</h1><p>A Broccoly outlet verification application has been submitted.</p><table><tr><td><b>Outlet</b></td><td>{H("OutletName")}</td></tr><tr><td><b>Owner</b></td><td>{H("OwnerName")}</td></tr><tr><td><b>Email</b></td><td>{H("Email")}</td></tr><tr><td><b>Plan</b></td><td>{H("PlanName")}</td></tr><tr><td><b>Submitted</b></td><td>{H("SubmittedAtUtc")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.OutletVerificationApproved => Build(
                "Your Broccoly outlet has been approved",
                $"Hello {V("OwnerName")},\n\nGreat news — {V("OutletName")} has been approved.\n\nCustomer storefront: {V("StorefrontUrl")}\nOutlet admin portal: {V("OutletAdminUrl")}\nPlan: {V("PlanName")}\n\nComplete your branding, menu, delivery areas, customer policies and operational settings before going live.\n\n{footerText}",
                $"<h1>Outlet approved ✓</h1><p>Hello {H("OwnerName")}, great news — <b>{H("OutletName")}</b> has been approved.</p><p><a href=\"{H("OutletAdminUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Open outlet admin →</a></p><p>Customer storefront: <a href=\"{H("StorefrontUrl")}\" style=\"color:#1d6b3a\">{H("StorefrontUrl")}</a></p><p>Plan: {H("PlanName")}</p><p>Complete your branding, menu, delivery areas, customer policies and operational settings before going live.</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.OutletVerificationRejected => Build(
                "Broccoly verification needs attention",
                $"Hello {V("OwnerName")},\n\nYour Broccoly outlet application for {V("OutletName")} was not approved at this time.\n\nReview notes: {V("Notes")}\n\nPlease update the required information or documents and resubmit.\n\nOutlet admin portal: {V("OutletAdminUrl")}\n\n{footerText}",
                $"<h1>Verification needs attention</h1><p>Hello {H("OwnerName")}, your Broccoly outlet application for <b>{H("OutletName")}</b> was not approved at this time.</p><div style=\"background:#fff4f2;border:1px solid #f0d1cb;border-radius:12px;padding:14px\"><b>Review notes</b><br/>{H("Notes")}</div><p>Update the required information or documents and resubmit.</p><p><a href=\"{H("OutletAdminUrl")}\" style=\"color:#1d6b3a\">Open outlet admin →</a></p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.PackageCreated => Build(
                $"New meal package from {V("OutletName")}",
                $"Hello {V("FirstName")},\n\n{V("OutletName")} has created a meal package for you.\n\nPackage: {V("PackageName")}\nMeals: {V("MealCount")}\nDates: {V("StartDate")} to {V("EndDate")}\nTotal: {V("Total")}\n\nReview and accept the package from your customer storefront: {V("StorefrontUrl")}\n\n{footerText}",
                $"<h1>Your meal package is ready</h1><p>Hello {H("FirstName")}, <b>{H("OutletName")}</b> has created a meal package for you.</p><table><tr><td><b>Package</b></td><td>{H("PackageName")}</td></tr><tr><td><b>Meals</b></td><td>{H("MealCount")}</td></tr><tr><td><b>Dates</b></td><td>{H("StartDate")} to {H("EndDate")}</td></tr><tr><td><b>Total</b></td><td>{H("Total")}</td></tr></table><p><a href=\"{H("StorefrontUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Review package →</a></p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.PackageAccepted => Build(
                $"Package accepted by {V("CustomerName")}",
                $"Customer {V("CustomerName")} has accepted the package created by {V("OutletName")}.\n\nPackage: {V("PackageName")}\nMeals: {V("MealCount")}\nTotal: {V("Total")}\nStatus: Payment pending\n\nOpen outlet admin: {V("OutletAdminUrl")}\n\n{footerText}",
                $"<h1>Package accepted ✓</h1><p><b>{H("CustomerName")}</b> accepted the package created by <b>{H("OutletName")}</b>.</p><table><tr><td><b>Package</b></td><td>{H("PackageName")}</td></tr><tr><td><b>Meals</b></td><td>{H("MealCount")}</td></tr><tr><td><b>Total</b></td><td>{H("Total")}</td></tr><tr><td><b>Status</b></td><td>Payment pending</td></tr></table><p><a href=\"{H("OutletAdminUrl")}\" style=\"color:#1d6b3a\">Open outlet admin →</a></p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.PackagePaymentConfirmed => Build(
                $"Payment confirmed for {V("PackageName")}",
                $"Hello {V("FirstName")},\n\nYour payment for {V("PackageName")} at {V("OutletName")} has been confirmed.\n\nAmount: {V("Total")}\nPayment method: {V("PaymentMethod")}\nStart date: {V("StartDate")}\n\nYour meals are now scheduled for delivery.\n\n{footerText}",
                $"<h1>Payment confirmed ✓</h1><p>Hello {H("FirstName")}, your payment for <b>{H("PackageName")}</b> at <b>{H("OutletName")}</b> has been confirmed.</p><table><tr><td><b>Amount</b></td><td>{H("Total")}</td></tr><tr><td><b>Payment method</b></td><td>{H("PaymentMethod")}</td></tr><tr><td><b>Start date</b></td><td>{H("StartDate")}</td></tr></table><p>Your meals are now scheduled for delivery.</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.SubscriptionCreated => Build(
                $"Subscription received from {V("OutletName")}",
                $"Hello {V("FirstName")},\n\nWe received your subscription for {V("OutletName")}.\n\nPlan: {V("PackageName")}\nMeals: {V("MealCount")}\nStart date: {V("StartDate")}\nTotal due: {V("Total")}\nPayment status: {V("PaymentStatus")}\n\n{footerText}",
                $"<h1>Subscription received</h1><p>Hello {H("FirstName")}, we received your subscription for <b>{H("OutletName")}</b>.</p><table><tr><td><b>Plan</b></td><td>{H("PackageName")}</td></tr><tr><td><b>Meals</b></td><td>{H("MealCount")}</td></tr><tr><td><b>Start date</b></td><td>{H("StartDate")}</td></tr><tr><td><b>Total due</b></td><td>{H("Total")}</td></tr><tr><td><b>Payment status</b></td><td>{H("PaymentStatus")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.MealSkipped => Build(
                $"Meal skipped — {V("MealDate")}",
                $"Hello {V("FirstName")},\n\nYour meal for {V("MealDate")} ({V("MealName")}) has been marked as skipped.\nReason: {V("Reason")}\nLate skip fee: {V("LateSkipFee")}\n\n{footerText}",
                $"<h1>Meal skipped</h1><p>Hello {H("FirstName")}, your meal for <b>{H("MealDate")}</b> ({H("MealName")}) has been marked as skipped.</p><table><tr><td><b>Reason</b></td><td>{H("Reason")}</td></tr><tr><td><b>Late skip fee</b></td><td>{H("LateSkipFee")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.MealRescheduled => Build(
                $"Meal rescheduled — {V("NewDate")}",
                $"Hello {V("FirstName")},\n\nYour meal {V("MealName")} has been rescheduled.\nOriginal date: {V("OldDate")}\nNew date: {V("NewDate")}\n\n{footerText}",
                $"<h1>Meal rescheduled</h1><p>Hello {H("FirstName")}, your meal <b>{H("MealName")}</b> has been rescheduled.</p><table><tr><td><b>Original date</b></td><td>{H("OldDate")}</td></tr><tr><td><b>New date</b></td><td>{H("NewDate")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.PasswordReset => Build(
                "Reset your Broccoly password",
                $"Hello {V("FirstName")},\n\nWe received a request to reset your Broccoly password.\n\nReset link: {V("ResetUrl")}\nThis link expires: {V("ExpiresAt")}\n\nIf you did not request this, you can ignore this email.\n\n{footerText}",
                $"<h1>Reset your password</h1><p>Hello {H("FirstName")}, we received a request to reset your Broccoly password.</p><p><a href=\"{H("ResetUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Reset password →</a></p><p>This link expires: {H("ExpiresAt")}</p><p>If you did not request this, you can ignore this email.</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.EmailVerification => Build(
                "Verify your Broccoly email address",
                $"Hello {V("FirstName")},\n\nPlease verify your email address by opening: {V("VerificationUrl")}\n\nThis link expires: {V("ExpiresAt")}\n\n{footerText}",
                $"<h1>Verify your email</h1><p>Hello {H("FirstName")}, please verify your email address.</p><p><a href=\"{H("VerificationUrl")}\" style=\"display:inline-block;background:#1d6b3a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:9px;font-weight:700\">Verify email →</a></p><p>This link expires: {H("ExpiresAt")}</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.DeliveryReminder => Build(
                $"Delivery reminder — {V("DeliveryDate")}",
                $"Hello {V("FirstName")},\n\nReminder: your {V("MealName")} delivery is scheduled for {V("DeliveryDate")} during {V("DeliveryWindow")}.\nAddress: {V("Address")}\n\n{footerText}",
                $"<h1>Delivery reminder</h1><p>Hello {H("FirstName")}, your meal delivery is scheduled for <b>{H("DeliveryDate")}</b> during <b>{H("DeliveryWindow")}</b>.</p><p>{H("Address")}</p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.PaymentFailed => Build(
                "Broccoly payment needs attention",
                $"Hello {V("FirstName")},\n\nYour payment for {V("PackageName")} could not be completed.\nAmount: {V("Total")}\nReason: {V("Reason")}\n\nPlease retry the payment from your customer storefront.\n{V("StorefrontUrl")}\n\n{footerText}",
                $"<h1>Payment needs attention</h1><p>Hello {H("FirstName")}, your payment for <b>{H("PackageName")}</b> could not be completed.</p><table><tr><td><b>Amount</b></td><td>{H("Total")}</td></tr><tr><td><b>Reason</b></td><td>{H("Reason")}</td></tr></table><p><a href=\"{H("StorefrontUrl")}\" style=\"color:#1d6b3a\">Retry payment →</a></p>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.RefundProcessed => Build(
                "Your Broccoly refund has been processed",
                $"Hello {V("FirstName")},\n\nYour refund for {V("PackageName")} has been processed.\nRefund amount: {V("RefundAmount")}\nReference: {V("Reference")}\n\n{footerText}",
                $"<h1>Refund processed</h1><p>Hello {H("FirstName")}, your refund for <b>{H("PackageName")}</b> has been processed.</p><table><tr><td><b>Refund amount</b></td><td>{H("RefundAmount")}</td></tr><tr><td><b>Reference</b></td><td>{H("Reference")}</td></tr></table>{footerHtml}",
                options.ReplyToAddress),

            EmailTemplateId.SaaSRenewalReminder => Build(
                $"Broccoly subscription renewal — {V("RenewalDate")}",
                $"Hello {V("OwnerName")},\n\nYour Broccoly SaaS subscription for {V("OutletName")} renews on {V("RenewalDate")}.\nPlan: {V("PlanName")}\nAmount: {V("Amount")}\n\nOutlet billing: {V("BillingUrl")}\n\n{footerText}",
                $"<h1>SaaS renewal reminder</h1><p>Hello {H("OwnerName")}, your Broccoly SaaS subscription for <b>{H("OutletName")}</b> renews on <b>{H("RenewalDate")}</b>.</p><table><tr><td><b>Plan</b></td><td>{H("PlanName")}</td></tr><tr><td><b>Amount</b></td><td>{H("Amount")}</td></tr></table><p><a href=\"{H("BillingUrl")}\" style=\"color:#1d6b3a\">Open billing →</a></p>{footerHtml}",
                options.ReplyToAddress),

            _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unsupported email template.")
        };
    }

    private static RenderedEmail Build(string subject, string text, string content, string? replyTo)
    {
        var html = $"<!doctype html><html><body style=\"margin:0;background:#f4f8f4;font-family:Arial,Helvetica,sans-serif;color:#173b24\"><div style=\"max-width:640px;margin:0 auto;padding:28px 16px\"><div style=\"background:#ffffff;border:1px solid #dce8df;border-radius:16px;padding:26px\"><div style=\"font-size:20px;font-weight:800;color:#1d6b3a;letter-spacing:-.02em\">BROCCOLY</div><div style=\"font-size:10px;letter-spacing:.12em;color:#7a887f;font-weight:700;margin-top:3px\">HEALTHY FOOD BUSINESS SAAS</div><div style=\"margin-top:24px;color:#173b24\">{content}</div></div></div></body></html>";
        return new RenderedEmail(subject, text, html, replyTo);
    }
}
