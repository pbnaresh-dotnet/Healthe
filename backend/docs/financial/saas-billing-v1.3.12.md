# Finance Policy v1.3.12 — SaaS Collections and Shared Payment Processing

- **Version:** 1.3.12
- **Status:** Implementation baseline; production approval requires finance/CA review and SQL Server concurrency tests
- **Effective date:** 2026-10-10
- **Supersedes:** v1.3.11
- **Scope:** Broccoly platform setup and recurring SaaS subscription invoices, plus shared provider-payment processing reliability used by customer subscriptions. This is not a tax-calculation policy for outlet-to-customer meal/package billing.

## 1. Cashfree SaaS payment-link callbacks

- A successful Cashfree payment-link callback must include the provider's actual payment transaction ID. The payment-link ID identifies the payment request and is not substituted for a missing payment ID.
- The rounded provider-reported amount must exactly match the immutable amount registered for the payment link. Currency is checked before collection posting.
- Link-scoped idempotency protects callback retries for one link. A second check validates the provider payment transaction reference across invoices under update/serializable key-range locking.
- If the provider payment ID was already allocated to the same invoice for the same amount, the callback is acknowledged as a duplicate without another payment row or balance change.
- If the reference exists with a different amount or was allocated to another invoice, the callback is rejected for manual reconciliation. The discrepancy is audited.
- The unique database index on provider plus non-empty, non-manual payment reference remains the final integrity barrier. Application checks do not replace database uniqueness.

## 2. Shared customer-payment webhook and polling concurrency

- Both the provider webhook and authenticated payment-status polling acquire an atomic processing lease through a conditional SQL update before querying provider status or triggering fulfilment.
- Only one request can own a payment's live processing lease. Other simultaneous requests return an already-processing result and do not call the provider or fulfil the payment again.
- The lease records its acquisition time. If a process terminates unexpectedly while holding the lease, another request may reclaim it after five minutes. The timeout is a recovery mechanism, not a guarantee that an unusually long-running process cannot overlap; production monitoring and integration tests remain required.
- While the lease is held, the handler verifies the provider's current transaction status, amount and currency. The lease is preserved when verified fields are persisted.
- A successful payment replay re-runs state-guarded package activation or onboarding completion. This repairs the case where payment verification committed but the process stopped before fulfilment.
- A provider/fulfilment exception releases the live lease into a retryable failure state. A process crash leaves the lease reclaimable after the timeout.
- The payment transaction's unique provider/payment-ID index remains an additional database-level defence against allocating one provider payment ID to multiple payment records.

## 3. Idempotency and operational boundaries

- Database state transitions and subscription/onboarding state guards are designed to be replay-safe. Email delivery is an external side effect and is not guaranteed exactly-once by the processing lease; an interruption after sending but before persisting completion may require duplicate-email tolerance or a future transactional outbox.
- The lease is held while provider status and fulfilment are processed. Keep these operations bounded and monitor expired-lease recovery.
- Do not log provider secrets, raw webhook bodies, payment credentials or unnecessary customer data.
- Manual reconciliation must compare the provider transaction record with the existing payment reference and invoice/subscription state before changing financial records.

## 4. Required production tests

1. Two simultaneous callbacks for the same provider order: only one owns the live lease.
2. A callback racing an authenticated status poll: only one owns the live lease.
3. A repeated callback after a completed payment: fulfilment remains state-guarded and no second collection is created.
4. A crash/stale lease: another request can reclaim after the five-minute window.
5. Provider status, amount or currency mismatch: no successful fulfilment.
6. A provider payment ID reused against another payment record: the unique index rejects the duplicate allocation.
7. Interruption after marking a payment Paid but before activation/onboarding completion: a later paid replay repairs fulfilment.
8. Email interruption around onboarding completion: confirm the accepted duplicate-delivery behavior and reconcile notification state.

## Change log

- **v1.3.12 — 2026-10-10:** Add atomic expiring payment-processing leases across Cashfree webhooks and customer payment-status polling; replay state-guarded fulfilment; document retry and notification boundaries.
- **v1.3.11 — 2026-10-10:** Require Cashfree's actual payment transaction ID and guard duplicate provider references across separate links and invoices.
- **v1.3.10 — 2026-10-10:** Normalize manual receipts before validation/persistence and serialize concurrent Cashfree payment-link webhook allocations using invoice locks.
