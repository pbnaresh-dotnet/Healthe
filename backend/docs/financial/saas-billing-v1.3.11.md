# SaaS Billing & Collections — Finance Policy v1.3.11

- **Version:** 1.3.11
- **Status:** Implementation baseline; subject to finance/CA review before production invoicing
- **Effective date:** 2026-10-10
- **Supersedes:** v1.3.10
- **Scope:** Broccoly platform setup and recurring SaaS subscription invoices. This is not outlet-to-customer meal/package billing.

## 1. Provider payment identity

- A paid Cashfree payment-link webhook must include the provider's actual payment transaction ID. The payment-link ID identifies the payment request, not the money movement, and must not be used as a substitute when the payment ID is missing.
- Invoice-scoped idempotency for the payment link remains in place. Before a new collection is inserted, the webhook also checks the provider + payment transaction reference against existing SaaS collections under update/serializable key-range locking.
- If the provider transaction reference was already allocated to the same invoice with the same amount, the callback is acknowledged as a duplicate and no second payment row or balance change is created. If the reference exists with a different amount, it is treated as a reconciliation conflict.
- If that provider reference is already allocated to a different invoice, the callback is rejected for manual reconciliation. The conflict is audited and the invoice ledger is not changed.
- The unique database index on (Provider, Reference) for non-manual, non-empty references remains the final integrity barrier across concurrent requests. The application check improves deterministic handling; it does not replace the unique index.

## 2. Existing link and invoice controls

- The callback validates the HMAC signature over timestamp + raw request body, resolves the registered Cashfree link, checks currency, and requires a paid status.
- The rounded provider-reported amount must equal the immutable amount recorded for the link. A mismatch is rejected without posting a collection.
- The database transaction locks the registered link and invoice while checking link idempotency and the remaining balance, inserting the collection, updating invoice balances and marking the link paid.
- The invoice lock serializes callbacks for separate links associated with the same invoice. A callback that would exceed the remaining balance is rejected for manual reconciliation.
- Invoice snapshots remain immutable after collection. Payment rows, invoice balance updates and audit entries for an accepted collection are committed transactionally.

## 3. Audit and operational handling

- A repeated provider transaction reference on the same invoice is recorded as an ignored duplicate; a cross-invoice reference collision is recorded as a reconciliation conflict.
- Missing provider payment IDs and cross-invoice conflicts never create another payment row or change invoice balances.
- Provider secrets and raw webhook bodies must not be written to application logs. Reconciliation staff should compare the provider dashboard/settlement statement against the invoice and existing payment reference before any manual correction.

## 4. Tax and production limitations

- The SaaS billing default tax rate remains configured through Finance:SaaSBillingTaxRatePercent (18% default pending CA review). Applied rate and aggregate tax are snapshotted.
- This implementation does not yet derive statutory CGST/SGST/IGST components, SAC or all place-of-supply requirements. Finance/CA review is required before generated documents are used as statutory tax invoices.
- Production verification must include SQL Server concurrency tests for duplicate callbacks, same payment ID arriving through different links, cross-invoice reference collision, two different payment links on one invoice, amount/currency mismatch, stale balances and sub-cent manual amounts.

## Change log

- **v1.3.11 — 2026-10-10:** Require Cashfree's actual payment transaction ID and guard duplicate provider references across separate links and invoices.
- **v1.3.10 — 2026-10-10:** Normalize manual receipts before validation/persistence and serialize concurrent Cashfree payment-link webhook allocations using invoice locks.
- **v1.3.9 — 2026-10-10:** Require exact registered payment-link amount matching before automatic collection posting.
