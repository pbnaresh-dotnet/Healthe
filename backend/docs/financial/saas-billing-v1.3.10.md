# SaaS Billing & Collections — Finance Policy v1.3.10

- **Version:** 1.3.10
- **Status:** Implementation baseline; subject to finance/CA review before production invoicing
- **Effective date:** 2026-10-10
- **Supersedes:** v1.3.9
- **Scope:** Broccoly platform setup and recurring SaaS subscription invoices. This is not outlet-to-customer meal/package billing.

## 1. Collection amount normalization

- Every manual receipt is rounded to two decimal places using midpoint-away-from-zero before validation, persistence, invoice allocation and audit logging.
- A receipt that rounds to less than INR 0.01 is rejected. This prevents zero-value payment rows and ensures the amount checked against the outstanding balance is exactly the amount stored.
- A receipt cannot exceed the outstanding balance after rounding. The accepted payment, new invoice amount-paid/balance, and audit amount use the same rounded value.
- Idempotency keys remain invoice-scoped for manual receipts. A repeated key returns the original payment result rather than posting another receipt.

## 2. Cashfree payment-link callback concurrency

- The callback validates the HMAC signature over timestamp + raw request body, resolves the registered Cashfree link, checks currency and requires a paid status.
- The rounded provider-reported amount must equal the immutable amount recorded for the link. A mismatch is rejected for manual reconciliation without changing the invoice balance.
- The database transaction acquires update/serializable locks for both the registered link and its invoice while checking idempotency and the remaining balance, inserting the collection, updating invoice balances and marking the link paid.
- The invoice lock serializes callbacks for separate payment links associated with the same invoice. A later callback cannot allocate against a stale pre-payment balance; if the invoice has insufficient balance, automatic posting is rejected and the provider transaction must be reconciled manually.
- Repeated callback delivery is idempotency-guarded. A link creation or email delivery is not proof of collection.

## 3. Audit and immutable documents

- Invoice snapshot JSON and its SHA-256 hash remain unchanged when payments are collected.
- Collection records and audit events are transactional with invoice balance updates. Rejected amount validations create no payment record.
- Provider secrets and raw webhook bodies must not be written to application logs.

## 4. Tax and production limitations

- The SaaS billing default tax rate remains configured through `Finance:SaaSBillingTaxRatePercent` (18% default pending CA review). Applied rate and aggregate tax are snapshotted.
- This implementation does not yet derive statutory CGST/SGST/IGST components, SAC or all place-of-supply requirements. Finance/CA review is required before generated documents are used as statutory tax invoices.
- Production verification must include SQL Server concurrency tests for two different payment links on one invoice, duplicate callbacks, amount/currency mismatch, stale balances, sub-cent manual amounts, and Area Manager authorization.

## Change log

- **v1.3.10 — 2026-10-10:** Normalize manual receipts before validation/persistence and serialize concurrent Cashfree payment-link webhook allocations using invoice locks.
- **v1.3.9 — 2026-10-10:** Require exact registered payment-link amount matching before automatic collection posting.
