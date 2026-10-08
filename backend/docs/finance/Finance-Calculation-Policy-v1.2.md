# HealthApp Finance Calculation & Accounting Policy
Version: 1.2
Effective from: 2026-10-08 UTC
Status: Published implementation version
Implementation phase: FIN-Phase-2C-Snapshots

> Product implementation record only; CA/tax adviser approval remains required for production GST/SAC/Section 9(5) treatment.

## Change from v1.1
Final transaction calculations now create immutable finance calculation snapshots.

Each snapshot records:
- outlet tax profile identifier;
- restaurant and platform tax rule identifiers;
- finance policy version used;
- tax applicability and pricing mode;
- calculation inputs and results;
- commission base/rate/amount;
- a deterministic input hash and calculation timestamp.

## Audit trace
Business event → effective outlet tax profile → effective tax rules → calculation → immutable calculation snapshot → financial document → payment allocation → settlement/refund → ledger → reporting.

Historical finance explanations use the snapshot captured at transaction time rather than today's live configuration.

## Immutability
Published policy versions and transaction calculation snapshots are not edited to correct history. A new policy version or corrective financial document is created when a business rule changes.

## CA/accountant approval boundary
Exact GST registration/composition treatment, restaurant classification, ECO Section 9(5), SAC, place of supply, invoicing and return reporting must be reviewed by the business's qualified adviser.

Source code reference: FIN-Phase-2C-Snapshots
Previous policy: v1.1