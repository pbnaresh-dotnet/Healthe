# Cashfree Payments

Broccoly uses one server-side payment abstraction for outlet onboarding and customer subscription payments. Cashfree credentials are never exposed to React/Vite builds.

## Backend configuration

Set these application settings on the API server:

- Cashfree__Enabled=true
- Cashfree__Environment=Sandbox (or Production)
- Cashfree__ClientId=<Cashfree app ID>
- Cashfree__ClientSecret=<Cashfree secret key>
- Cashfree__ApiVersion=2025-01-01
- Customer payment return URLs are generated server-side from the outlet storefront domain (primary active custom domain, otherwise `<subdomain>.broccoly.in`)
- Cashfree__OutletReturnUrl=https://broccoly.in/payment
- Cashfree__WebhookUrl=https://api.broccoly.in/api/payments/webhook

For Azure App Service, prefer Key Vault references or App Service Application Settings. Never put the secret in a VITE_* variable, source code, git history, or frontend environment.

For local development, use dotnet user-secrets or operating-system environment variables.

## Frontend configuration

The React applications only need the public Cashfree mode:

- VITE_CASHFREE_MODE=sandbox

Keep VITE_CASHFREE_MODE=production only when the API is configured with the matching Cashfree production credentials.

## Cashfree dashboard

Configure the Cashfree Payment Gateway webhook/notify URL to:

https://api.broccoly.in/api/payments/webhook

The API verifies the Cashfree webhook signature against the raw request body. The application also queries Cashfree server-side before marking a payment successful.

## Payment lifecycle

1. Broccoly creates its own pending payment transaction.
2. Broccoly creates the Cashfree order server-side.
3. The frontend receives only the payment session ID and opens Cashfree Checkout.
4. Cashfree sends a webhook after the payment attempt.
5. Broccoly verifies the webhook signature and fetches the provider payment status.
6. Amount and currency are checked against the Broccoly transaction.
7. Only a verified successful payment can activate the customer package or move outlet onboarding to PendingVerification.

The browser callback is never treated as proof of payment.

## Sandbox to production

Use the Cashfree Sandbox environment first. After the end-to-end flow is verified:

- switch Cashfree Dashboard to the live/production configuration,
- set Cashfree__Environment=Production,
- set Cashfree__Enabled=true,
- set VITE_CASHFREE_MODE=production,
- configure the production webhook URL,
- verify the live webhook is reaching the API,
- run a low-value live transaction before enabling normal customer traffic.

## SaaS invoice payment links

SaaS invoice payment links are separate from customer meal/package checkout. The embedded Super Admin app creates a Cashfree Payment Link for the invoice's current outstanding balance and emails it to the active Outlet Admin address when requested.

- Configure `Cashfree__Enabled=true`, `Cashfree__ClientId`, `Cashfree__ClientSecret`, and `Cashfree__Environment` on the API server. SMTP must also be configured with `Email__Enabled=true` and secret-backed `Email__Host`, `Email__Username`, `Email__Password`, and `Email__FromAddress`.
- Set `Cashfree__SaaSPaymentLinkWebhookUrl=https://api.broccoly.in/api/saas-billing/cashfree/payment-link-webhook` in server-side configuration.
- Configure a Cashfree payment-link webhook in the Cashfree dashboard to that URL. Keep the existing general payment webhook at `https://api.broccoly.in/api/payments/webhook` for the existing customer/onboarding gateway flows.
- The API validates Cashfree webhook signatures and posts a successful payment to the invoice collection ledger idempotently. Never mark an invoice paid from a browser redirect alone.
- A link expires after the selected validity window (default seven days, max 30). If email fails, the admin sees the failure and can copy/send the generated link manually.

Use the Sandbox environment to validate link creation, email delivery, successful/failed/expired webhook events, duplicate callbacks and invoice balance reconciliation before switching to Production.
