# Broccoly Email & SMTP

## SMTP configuration

SMTP credentials are intentionally not stored in source control. Configure the following environment variables in local development or Azure App Service:

- Email__Enabled=true
- Email__Host=<smtp-host>
- Email__Port=587
- Email__EnableSsl=true
- Email__Username=<smtp-username>
- Email__Password=<smtp-password>
- Email__FromAddress=<verified-sender@your-domain>
- Email__FromName=Broccoly
- Email__ReplyToAddress=support@broccoly.in
- Email__AdminNotificationAddress=<internal-verification-team@your-domain>
- Email__SupportAddress=support@broccoly.in
- Email__OutletAdminUrl=https://outlet.broccoly.in
- Email__TimeoutSeconds=20

The exact SMTP host and credentials depend on the provider you use. Use a provider/domain where the sender address is verified and authenticated (SPF/DKIM/DMARC).

### Azure App Service

Add the same names under Configuration → Application settings. Azure App Service will map Email__Host etc. to the Email:Host configuration section.

Do not put the SMTP password in appsettings.Production.json, GitHub Actions YAML, or frontend .env files.

## SMTP smoke test

A Super Admin can call:

POST /api/admin/email/test?to=<recipient>

The endpoint uses the versioned SMTP test template and returns 503 when SMTP is disabled or not configured.

## Transactional template catalog

Templates are versioned in code as v1 and rendered as both HTML and plain text:

| Template | Used for |
|---|---|
| SmtpTest | SMTP verification |
| OutletDemoAccess | 7-day demo credentials |
| CustomerWelcome | Customer registration |
| OutletOnboardingPaymentConfirmed | Broccoly setup payment received |
| OutletVerificationSubmitted | Outlet submitted for verification |
| OutletVerificationApproved | Outlet approved |
| OutletVerificationRejected | Verification requires changes |
| PackageCreated | Outlet sends a package to a customer |
| PackageAccepted | Customer accepts an outlet-created package |
| PackagePaymentConfirmed | Customer package/subscription payment succeeds |
| SubscriptionCreated | Customer creates a subscription |
| MealSkipped | Customer skip confirmation |
| MealRescheduled | Customer reschedule confirmation |
| PasswordReset | Password reset flow (template ready) |
| EmailVerification | Email verification flow (template ready) |
| DeliveryReminder | Upcoming delivery reminder (template ready) |
| PaymentFailed | Payment failure notification (template ready) |
| RefundProcessed | Refund notification (template ready) |
| SaaSRenewalReminder | Outlet SaaS renewal reminder (template ready) |

## Implemented triggers

The current build sends transactional emails for demo creation, customer registration, outlet onboarding payment confirmation, outlet verification submission, outlet verification approval/rejection, outlet-created package creation, customer acceptance of an outlet-created package, successful package/subscription payment, customer-created subscription, meal skip, and meal reschedule.

Business operations are not rolled back when a non-critical notification fails; those failures are logged as warnings. Demo credential delivery remains a required operation because the generated temporary password is delivered by email.

## Production hardening

The email sender uses UTF-8 HTML + plain-text alternatives, configurable SMTP timeout, configurable sender/reply-to addresses, centralized HTML escaping for template values, versioned templates, no SMTP secrets in Git, and an admin-only delivery smoke test.

