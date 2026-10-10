# SaaS Billing & Collections — Finance Policy v1.3.8

- **Version:** 1.3.8
- **Status:** Implementation baseline; subject to finance/CA review before production invoicing
- **Effective date:** 2026-10-10
- **Supersedes:** v1.3.7
- **Scope:** Outlet setup and recurring SaaS subscription invoices issued by the Broccoly platform. This is not an outlet's customer meal/package billing.

## 1. Authority and access

1. Super Admin can generate invoices for all eligible outlets and set the invoice tax-rate override.
2. Area Managers can generate invoices and record collections only for outlets present in `AreaManagerOutletAssignments`. Outlet groups are reporting classifications, never authorization boundaries.
3. API endpoints enforce the same scope server-side. The browser's outlet filter is not an authorization control.
4. Invoice and collection actions are audited with actor, UTC timestamp, action and structured details.

## 2. Invoice period and duplicate protection

- Billing period format is `YYYY-MM`; issue date is the first UTC day of that month and due day is selected between 1 and 28.
- Eligible subscriptions are active and linked to a non-pending, non-suspended outlet.
- Monthly plans are considered every month. Six-month plans are considered on six-month anniversary months. Annual plans are considered in the subscription start month for subsequent years.
- The invoice register has a unique outlet + billing-period constraint for non-void invoices. Re-running a billing period skips existing invoices rather than creating duplicates.
- Monthly amount uses the configured plan monthly fee. Six-month and annual billing use the stored subscription fee or configured annual fee respectively.
- Setup fee is included only in the subscription's start month. The one-time onboarding discount is applied to that initial invoice only; it is not repeated against recurring monthly dues.
- Before production use, validate the eligibility rule against subscription start/renewal dates and business decisions for trial conversion, reactivation, paused subscriptions, and plan changes.

## 3. Snapshot and integrity

Every issued invoice stores a JSON snapshot including outlet and subscription identity, plan name, billing period, line descriptions, amounts, discount, tax rate, tax amount, total, currency, issue/due dates and finance-policy version. The application calculates a SHA-256 hash over the exact serialized snapshot. The issued snapshot is not editable through this API; corrections must be implemented as an auditable credit/adjustment workflow, not an in-place rewrite.

## 4. Tax handling

- The platform's known SaaS-charge GST baseline is 18% (CGST/SGST or IGST split is determined by the platform's approved tax profile/place-of-supply rules).
- The backend configuration key `Finance:SaaSBillingTaxRatePercent` controls the default Area Manager rate and defaults to 18% when absent.
- Super Admin may enter a tax-rate override for a billing run. Each invoice snapshots the applied rate and amount.
- This module currently calculates a single aggregate tax amount; it does **not** yet derive or issue the legally complete CGST/SGST/IGST components from platform tax profiles. Finance/CA must validate place of supply, supplier registration, SAC, invoice series, and tax components before using generated documents as statutory tax invoices. Do not represent the current HTML receipt as a tax invoice.

## 5. Collection and partial payment

- Payments are append-only rows. Each record captures amount, method, reference, notes, received timestamp, recorder, provider, and whether an online payment was verified.
- Supported methods: Cash, UPI, bank transfer, Other, and Online (provider reconciled).
- Cash reference may be blank. Non-cash methods require a reference. A payment cannot exceed the current outstanding balance; row locking serializes collection attempts against the invoice.
- An invoice is `Issued`, `PartiallyPaid`, or `Paid` according to the balance. A due invoice is displayed as `Overdue` when unpaid/partially paid and its due date has passed.
- Online collection in this module is a **reconciliation record**, not a gateway checkout. The operator must verify the transaction in the provider dashboard before checking the confirmation box. This UI does not initiate payment or verify provider webhooks; never mark an online payment verified based only on customer-provided text.
- Receipts are printable HTML and can be saved as PDF by the operator. They are collection receipts, not GST invoices.
- Payment rows are not edited or deleted by these endpoints. Refunds/reversals require a separate, auditable refund/credit workflow.

## 6. Audit and database

The idempotent database initializer creates `SaaSInvoices`, `SaaSInvoicePayments`, and `SaaSBillingAudit`, with outlet/subscription/payment foreign keys and indexes for billing period, status, due date, and payment history. The audit stores action, JSON details, actor ID and UTC time. The invoice snapshot hash supports integrity checks but does not replace database access controls, backups, signed artifacts, or independent audit.

## 7. Outstanding production work

This commit is not a declaration of production readiness. Before issuing statutory invoices:
- add full FinanceTaxRule/PlatformTaxProfile resolution and separate CGST/SGST/IGST components;
- align eligibility and proration with actual subscription renewal/plan-change/reactivation semantics;
- add a formal void/credit-note workflow and invoice-number sequence policy;
- verify online payments from signed provider callbacks/API reconciliation rather than a user-entered checkbox;
- expand reconciliation reports and ledger journal postings (payment collection idempotency keys are implemented per invoice);
- test authorization isolation, concurrency, duplicate generation, rounding and audit completeness;
- build and integration-test against a disposable SQL Server database.


## 8. Payment links and email collection

- Super Admin and assigned Area Managers can generate an on-demand Cashfree payment link from an outstanding invoice. The amount is the current invoice balance, not the original gross amount.
- The link is associated with the invoice, outlet-admin email, amount, currency, provider link ID, creator and expiry. Link creation and the email outcome are tracked in `SaaSInvoicePaymentLinks`; the creation is audited.
- The outlet's active `OutletAdmin` account email is the recipient. If there is no valid active outlet-admin email, link generation fails rather than silently sending to an unrelated address.
- Invoice email includes outlet name/city, invoice number, period, outstanding amount, due date, secure payment URL and expiry. Email uses the configured SMTP `IEmailService`; SMTP credentials remain server-side in environment/secret configuration.
- Cashfree credentials remain server-side. Configure `Cashfree:SaaSPaymentLinkWebhookUrl` to `https://api.broccoly.in/api/saas-billing/cashfree/payment-link-webhook` and configure the corresponding Cashfree payment-link webhook. The callback validates the Cashfree HMAC signature over timestamp + raw body, verifies link identity/currency, prevents duplicate posting, and records successful payments into the invoice collection ledger.
- The current implementation expects a full-paid link status of `PAID`; non-paid events are acknowledged without accounting changes. If a provider event reports an amount exceeding the current invoice balance, the callback rejects automatic posting for manual reconciliation.
- Repeated generation can create multiple active links for the same balance. Outlet should pay only one; overpayment protection and idempotency guard the invoice ledger. A future enhancement should automatically cancel/expire previous outstanding links when a new link is generated.
- A payment link is a collection mechanism, not an invoice. Invoice snapshot remains unchanged; the linked payments and audit history update the balance.
- Email delivery failure is reported to the admin and stored with the link record; the generated link remains available to copy/send manually.

## Change log

- **v1.3.8 — 2026-10-10:** Added on-demand Cashfree payment links per outstanding invoice, outlet-admin email delivery, link expiry and status tracking, signed payment-link webhook reconciliation, idempotent invoice payment posting, and audit records.\n- **v1.3.7 — 2026-10-10:** Added SaaS billing-period invoices, immutable JSON/hash snapshots, assigned Area Manager scoping, manual/verified-reconciled payment recording, partial balances, printable collection receipts, and audit records. Documented the remaining statutory tax/ledger/gateway-verification limitations.
