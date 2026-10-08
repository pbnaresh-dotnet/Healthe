# HealthApp Finance Calculation & Accounting Policy
Version: 1.1
Effective from: 2026-10-08 UTC
Status: Published implementation version
Implementation phase: FIN-Phase-2B-Tax-Engine

> Product implementation record only; CA/tax adviser approval remains required for production GST/SAC/Section 9(5) treatment.

## Changes from v1.0
- Effective-dated outlet tax profiles are resolved using the transaction date.
- Effective-dated finance tax rules are resolved before calculation.
- Direct + not GST registered => restaurant GST is zero.
- Direct + composition => restaurant GST is not added to the customer price.
- Inclusive/Exclusive is applied only after tax applicability is established.
- Missing/invalid tax rules fail closed rather than falling back to application settings.
- The calculation result carries profile/rule identifiers for transaction snapshots.

## Restaurant tax applicability
For DirectOutletSupplier mode, restaurant GST is applicable only when the outlet is GST registered and not configured as composition. For EcoSection9_5 mode, the ECO path is separately evaluated and may apply GST independently of supplier registration, subject to approved legal configuration.

## Inclusive/exclusive formula
If tax is not applicable, restaurant taxable amount equals the configured meal amount and restaurant GST is zero regardless of Inclusive/Exclusive.
For applicable Exclusive pricing: taxable = price; tax = taxable × rate / 100.
For applicable Inclusive pricing: taxable = gross × 100 / (100 + rate); tax = gross − taxable.

## Rule resolution
Restaurant tax uses the effective FinanceTaxRule for RestaurantSale at the calculation timestamp, preferring a mode-specific rule over a generic rule and then highest priority/effective version. Platform service GST uses the effective PlatformSaaS rule.

## Failure safety
The finance engine throws when an effective tax rule is missing or has invalid rates instead of silently using configuration-file fallbacks. This prevents an unreviewed hidden default from changing statutory calculations.

## Audit trace
Business event → effective outlet tax profile → effective finance tax rule → tax calculation → financial document → payment allocation → settlement/refund → ledger → reporting.

Source code reference: FIN-Phase-2B-Tax-Engine
Previous policy: v1.0

## CA/accountant approval boundary
Exact GST registration/composition treatment, restaurant classification, ECO Section 9(5), SAC, place of supply, invoicing and return reporting must be reviewed by the business's qualified adviser.