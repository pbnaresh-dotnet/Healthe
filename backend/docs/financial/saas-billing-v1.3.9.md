# SaaS Billing & Collections — Finance Policy v1.3.9

- **Version:** 1.3.9
- **Status:** Implementation baseline; subject to finance/CA review before production invoicing
- **Effective date:** 2026-10-10
- **Supersedes:** v1.3.8
- **Scope:** Broccoly platform setup and recurring SaaS subscription invoices. This is not outlet-to-customer meal/package billing.

## 1. Authority and invoice generation

- Super Admin can generate invoices for eligible outlets and apply a tax-rate override for a billing run.
- Area Managers can generate invoices and record collections only for outlets assigned in `AreaManagerOutletAssignments`; API-side authorization is authoritative.
- A billing period is `YYYY-MM`. A non-void invoice is unique per outlet and billing period. Repeated generation skips an existing invoice.
- Eligibility and plan pricing follow the subscription and SaaS plan values read at generation time. Before production use, confirm trial conversion, reactivation, pauses, plan changes and anniversary billing rules against business policy.

## 2. Snapshot, tax and rounding

- Each invoice stores an immutable JSON snapshot containing invoice/outlet/subscription identity, billing period, lines, discount, tax rate, tax amount, total, currency, issue/due timestamps and policy version. SHA-256 is calculated over the serialized snapshot.
- SaaS charge GST defaults to 18% through `Finance:SaaSBillingTaxRatePercent`, with a Super Admin override. The applied rate and amount are stored on the invoice.
- Tax is rounded to two decimal places using midpoint-away-from-zero rounding. This implementation currently stores aggregate tax only; it does not yet resolve statutory CGST/SGST/IGST components, SAC or all place-of-supply requirements. Finance/CA review is required before treating generated documents as statutory tax invoices.
- Historical invoice snapshots must not be rewritten to reflect later plan or tax configuration. Corrections require a separately audited void/credit/adjustment workflow.

## 3. Partial collections and receipts

- Payments are append-only and invoice-specific. The system records amount, method, reference, notes, receipt timestamp, recorder, provider verification and idempotency key.
- Cash, UPI, bank transfer, Other and provider-reconciled online collections are supported. Non-cash methods require a reference; partial receipts are allowed only up to the current balance.
- Invoice row locks serialize collection attempts; invoice balance/status are updated in the same database transaction as payment and audit records.
- Printable HTML receipts are collection receipts, not GST invoices. Refunds/reversals need a separate auditable workflow.

## 4. Cashfree payment links and webhook posting

- Super Admin and assigned Area Managers can generate an on-demand Cashfree link for the current outstanding balance. The link is recorded with provider link ID, invoice, currency, amount, recipient, expiry, creator and email outcome.
- Email delivery uses the configured server-side `IEmailService`; Cashfree credentials remain in server-side secret configuration. A generated link or sent email is not evidence of payment.
- The webhook validates the Cashfree HMAC signature over timestamp + raw body, resolves the registered provider link, validates currency and requires a `PAID` link status. Non-paid events do not post collections.
- **v1.3.9 amount control:** after currency validation, the rounded provider-reported amount must exactly equal the amount stored for that payment link. If it differs, the webhook returns a conflict and creates no payment row or invoice balance change; operations must reconcile the provider event manually. The amount is also checked against the invoice's remaining balance before posting.
- The webhook uses an invoice-scoped idempotency key and database transaction so a repeated event does not post a second collection. Link creation, successful posting and invoice audit events are persisted.
- If a customer pays an older link after a manual receipt or another link has reduced the invoice balance, the callback may reject automatic posting because the invoice no longer has enough balance. Reconcile the provider transaction manually rather than changing the immutable invoice or forcing a ledger posting.
- Repeated generation may create multiple links for the same balance. Outlet staff should pay only one link; automatic cancellation of prior links remains future work.

## 5. Audit and operational controls

- Invoice issuance, payment recording, payment-link creation and provider-confirmed collection are audited with action, actor where available, UTC timestamp and structured details.
- Provider transaction references are unique per provider. Manual receipt references may repeat and are not treated as globally unique provider transaction IDs.
- Payment-link amount/currency mismatches must not be silently accepted as partial payments. Reconcile the provider dashboard and retain evidence under the approved finance/audit procedure.
- Do not write Cashfree secrets or raw webhook bodies to application logs.

## 6. Outstanding production requirements

Before statutory or high-volume production use:
- resolve platform tax profiles and produce the appropriate CGST/SGST/IGST/SAC invoice components;
- confirm eligibility, renewal, proration and reactivation rules with Finance/CA;
- add a formal void/credit-note and invoice numbering workflow;
- reconcile provider webhook payload formats and signatures against Cashfree's configured production webhook;
- test duplicate/concurrent webhook handling, stale links, amount/currency mismatch, refunds and assigned Area Manager isolation against a disposable SQL Server database;
- build and run backend/frontend automated and end-to-end tests.

## Change log

- **v1.3.9 — 2026-10-10:** Require the provider-reported payment amount to match the registered payment-link amount before automatic collection posting; seed this version into the backend policy history and use it as the default invoice snapshot policy reference.
- **v1.3.8 — 2026-10-10:** Documented on-demand Cashfree payment links, outlet email delivery and signed payment-link webhook reconciliation.
- **v1.3.7 — 2026-10-10:** Added SaaS invoice snapshots, configurable tax rate, partial collections, payment records, receipts and audit history.
