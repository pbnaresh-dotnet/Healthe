# HealthApp Finance Calculation & Accounting Policy

Policy: Finance Calculation & Accounting Policy
Version: 1.0
Effective from: 2026-10-01 UTC
Status: Published baseline
Implementation phase: FIN-Phase-2A-Governance

> This is the product implementation record used to keep finance-related code, configuration and audit documentation aligned. It is not legal or tax advice. GST registration, composition treatment, Section 9(5) eligibility, SAC/classification, place of supply, invoicing and return reporting must be approved by the business's qualified tax/accounting adviser before production filing or reliance.

## 1. Finance governance and versioning
Finance logic and configuration are effective-dated. Published rules are not edited in place. A material finance change creates a new policy version, records the change summary, reason, effective date and source-code reference, and preserves the previous version.

Historical transactions must remain reproducible from the configuration/rule versions and financial-document snapshots that were used when the transaction was created.

Required audit chain:
Business event → tax calculation → financial document → payment allocation → settlement/refund → ledger → reporting

## 2. Four separate finance boundaries
The implementation keeps tax liability, economic revenue ownership, payment custody and settlement destination separate. A payment provider or bank destination does not by itself determine tax liability or revenue ownership.

## 3. Restaurant tax applicability
### Direct outlet supplier
- Outlet not GST registered: restaurant GST is not added to the customer meal/package price solely because a GST-inclusive pricing mode is selected. The configured customer meal/package price is the final restaurant price unless another applicable charge is explicitly configured.
- Outlet GST registered: the outlet may use GST-exclusive or GST-inclusive pricing according to its approved configuration.
- Inclusive pricing is a calculation/presentation mode, not a tax applicability switch.

### ECO Section 9(5)
ECO treatment is a separate operating mode. Its tax applicability and liability are assessed independently of the outlet's registration status. The system must not infer Section 9(5) treatment from payment settlement routing.

## 4. Inclusive/exclusive calculation
For an applicable tax rate r:

Exclusive: taxable = price; tax = taxable × r / 100

Inclusive: taxable = gross × 100 / (100 + r); tax = gross − taxable

Currency rounding is applied consistently and the final inputs/results are snapshotted.

Example for an applicable 5% inclusive amount of ₹1,000:
- Gross customer price: ₹1,000.00
- Taxable value: ₹952.38
- GST: ₹47.62
This example applies only where restaurant GST is actually applicable.

## 5. HealthApp platform charges
HealthApp setup fees, annual/SaaS fees and platform commissions are separate HealthApp supplies to the outlet. Their tax treatment is configured independently from restaurant GST.

Current product default: 18%, pending CA/tax-adviser approval of exact classification and SAC.

## 6. Platform commission
Product default commission basis is the restaurant taxable value, rather than restaurant GST. Commission basis is configurable. Any change must create a new finance policy version and document the revised formula and effective date.

## 7. Delivery and other charges
Delivery charges, late-skip fees, payment gateway fees and their taxes are separate financial components. Their tax treatment must be explicitly configured rather than inferred from restaurant GST.

## 8. Payment and settlement
Payment transactions represent payment-provider movement. Payment allocations link payments to financial documents. Gateway charges, gateway GST, platform deductions, settlement adjustments and refunds are separate financial records. Settlement destination is not the source of tax truth.

## 9. Refunds and credit/debit notes
Refunds are separate financial events linked to the original payment and financial document. Original issued financial documents are not rewritten. Corrections use credit/debit-note documents where legally required.

## 10. Double-entry ledger
Financial events are posted through balanced double-entry journals. Tax components may be linked to journal lines so tax liabilities can be reconciled from ledger to journal line to tax component to financial document to source business event.

## 11. Configuration principles
- explicit rather than inferred;
- outlet-scoped where the business rule belongs to the outlet;
- platform-scoped where the charge belongs to HealthApp;
- effective-dated;
- immutable after publication;
- snapshotted onto historical financial documents/calculations.

No finance calculation should depend on today's live configuration when explaining a historical transaction.

## 12. Change-control requirement
Whenever finance-related code changes:
1. Identify the affected policy section.
2. Create or update the corresponding versioned finance policy document.
3. Record the change summary, reason and effective date.
4. Record a source-code reference.
5. Preserve the previous policy version.
6. Ensure new transactions use the new effective configuration while historical transactions remain reproducible.

The backend database contains the authoritative versioned policy document and sections. This Markdown copy is the source-controlled implementation companion for CA/auditor review and code review.

## 13. CA/accountant approval boundary
This policy documents how the product is designed to calculate and record finance information. It does not replace professional advice or statutory filings.

The business CA/accountant must approve the production treatment of GST registration/composition status, restaurant GST applicability/rates, ECO Section 9(5), SAC/classification of HealthApp services, place-of-supply rules, invoice/credit-note requirements, settlement/payment-provider structure and GST return reporting mappings.

End of version 1.0