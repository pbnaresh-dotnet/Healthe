import React from 'react';

const RULE_GROUPS = [
  {
    eyebrow: '01 · OPERATING MODEL',
    title: 'Tax responsibility follows the selected operating mode',
    items: [
      ['Direct outlet-supplier mode', 'The outlet is the restaurant supplier to the customer. The outlet-side restaurant invoice and restaurant GST are separate from HealthApp\'s own SaaS/commission invoice.'],
      ['ECO Section 9(5) mode', 'The ECO invoice/tax flow is kept separate from the outlet\'s commercial settlement. Restaurant GST liability is tracked as the ECO\'s Section 9(5) liability; the outlet remains an underlying restaurant supplier for turnover/reporting.'],
      ['Important boundary', 'Payment custody, revenue ownership, tax liability and settlement destination are four different concepts. The system never infers tax liability from where money lands.']
    ]
  },
  {
    eyebrow: '02 · CALCULATION ORDER',
    title: 'Every charge is calculated from the underlying supply',
    items: [
      ['Step 1 · Identify the supply', 'Classify each amount as restaurant meal, delivery, platform SaaS, platform commission, setup fee, late-skip fee or another platform service.'],
      ['Step 2 · Resolve the effective rule', 'Use the outlet/platform tax profile and effective-dated finance tax rule for the supply date. Historical documents retain their own tax and party snapshots.'],
      ['Step 3 · Build the taxable base', 'Taxable value starts from the line gross value less eligible discounts. HealthApp commission is not deducted from the restaurant customer tax base.'],
      ['Step 4 · Apply inclusive/exclusive logic', 'Exclusive tax: Tax = taxable base × rate. Inclusive tax: Tax = gross tax-included amount × rate ÷ (100 + rate). The exact calculation is stored on the document, not recalculated from today\'s settings.'],
      ['Step 5 · Split components', 'For intra-state supplies, split the configured rate between CGST and SGST. For inter-state supplies, use IGST. Place-of-supply inputs drive the split; the payment gateway settlement account does not.'],
      ['Step 6 · Round and snapshot', 'Round monetary values at the documented financial boundary, then persist the tax components, rates, issuer/recipient details and place of supply so the document can be reproduced later.']
    ]
  },
  {
    eyebrow: '03 · CURRENT PRODUCT DEFAULTS',
    title: 'Defaults are configurable, not hidden constants',
    items: [
      ['Restaurant service', 'The product currently seeds a 5% restaurant GST rule as 2.5% CGST + 2.5% SGST intra-state, with a 5% IGST component for inter-state calculation.'],
      ['HealthApp services', 'The product currently seeds 18% for SaaS, platform commission and setup-fee supplies, with 9% CGST + 9% SGST intra-state and 18% IGST inter-state.'],
      ['Delivery', 'Delivery is intentionally not forced into the meal GST rule. Its tax classification is a separate rule/configuration and must be resolved before the transaction is posted.'],
      ['Legal/tax validation', 'These are product defaults for the finance engine, not a substitute for CA/legal confirmation of the exact SAC, restaurant classification, place-of-supply treatment or current GST notification/conditions.']
    ]
  },
  {
    eyebrow: '04 · HEALTHAPP COMMISSION',
    title: 'Commission is a separate platform supply',
    items: [
      ['Base', 'Commission is calculated from the configured commission base, normally the restaurant taxable amount before restaurant GST. The contract can later specify another base explicitly.'],
      ['Platform GST', 'GST on the HealthApp commission is a separate tax component and is not added to the restaurant GST.'],
      ['Settlement', 'Commission may be collected separately or deducted from an outlet settlement, but the accounting/tax document remains the same. A deduction changes settlement mechanics, not the restaurant sale value or restaurant GST base.']
    ]
  },
  {
    eyebrow: '05 · PAYMENTS & FEES',
    title: 'Gateway activity is recorded separately from tax',
    items: [
      ['Customer payment', 'A payment transaction represents the provider event. Payment allocations connect that payment to one or more financial documents.'],
      ['Gateway fee', 'Gateway charges and GST on gateway charges are recorded independently. Gateway GST is not restaurant GST and is not HealthApp output GST.'],
      ['Settlement', 'Settlement reconciles provider gross collections to the net bank/merchant payout using typed lines for gateway fees, platform deductions, refunds, tax deductions and adjustments.']
    ]
  },
  {
    eyebrow: '06 · REFUNDS & CREDIT NOTES',
    title: 'A refund is an accounting event, not a status toggle',
    items: [
      ['Original document remains', 'The original invoice/receipt is never rewritten to make the history disappear.'],
      ['Credit note + refund transaction', 'A refund creates an explicit credit note where required plus an immutable refund transaction tied to the original payment/document.'],
      ['Tax reversal', 'Restaurant GST refunds and platform GST refunds are carried as separate amounts and linked to the original tax components, so GST reporting can reverse the correct liability.']
    ]
  },
  {
    eyebrow: '07 · LEDGER',
    title: 'Double-entry is the accounting source of truth',
    items: [
      ['Journal rule', 'Every posted journal must balance: total debits = total credits.'],
      ['Examples', 'Customer collection uses gateway/bank clearing and the relevant receivable/revenue/tax accounts; gateway fees hit expense/input-tax accounts; outlet settlements hit outlet payable; platform commission hits HealthApp revenue.'],
      ['Legacy data', 'The existing subscription/order financial breakdown remains for backward compatibility during migration. It is not the long-term finance authority. The new ledger is designed to become authoritative in the transaction-posting phase.']
    ]
  },
  {
    eyebrow: '08 · OUTLET GST REPORTING',
    title: 'Reports are outlet-aware and mode-aware',
    items: [
      ['Direct mode', 'Report restaurant taxable value and restaurant GST as the outlet\'s supply, while HealthApp SaaS/commission GST remains platform-level revenue/tax reporting.'],
      ['ECO mode', 'Report the restaurant supply separately from HealthApp commission and flag Section 9(5) tax liability for ECO reporting. Settlement amount does not reclassify the tax liability.'],
      ['Period controls', 'Reports will group by outlet, tax mode, financial document date, place of supply, tax component and refund/credit-note status. Filing exports will be built from posted documents/journals, not UI totals.']
    ]
  }
];

const EXAMPLE_ROWS = [
  ['Restaurant taxable value', '₹10,000', '₹10,000'],
  ['Restaurant GST at 5%', '₹500', '₹500'],
  ['Customer restaurant total', '₹10,500', '₹10,500'],
  ['HealthApp commission at 3%', '₹300', '₹300'],
  ['GST on HealthApp commission at 18%', '₹54', '₹54'],
  ['Who carries restaurant GST liability?', 'Outlet', 'ECO under Section 9(5)'],
  ['Who issues the restaurant invoice?', 'Outlet', 'ECO']
];

export default function FinanceRulesHelp({onBack, onFinance}) {
  return <section className="financeRulesPage">
    <PageIntro
      eyebrow="FINANCE RULES & HELP"
      title="How finance calculations work"
      text="This page documents the calculation boundaries the product follows. Tax rates are effective-dated configuration, financial documents are snapshotted, and accounting is posted independently from payment settlement."
      action={<div className="financeRulesActions"><button className="secondaryBtn" onClick={onBack}>Back</button><button className="primaryBtn" onClick={onFinance}>Open finance</button></div>}
    />

    <div className="financeRulesNotice">
      <b>Production control:</b>
      <span>These are product calculation rules and system defaults. Exact GST classification, SAC, Section 9(5) eligibility, place-of-supply treatment and filing positions must be approved by the business\'s tax/accounting adviser before production use.</span>
    </div>

    <div className="financeRulesGrid">
      {RULE_GROUPS.map(group => <section className="card financeRuleCard" key={group.eyebrow}>
        <div className="cardHead"><div><span className="eyebrow">{group.eyebrow}</span><h2>{group.title}</h2></div></div>
        <div className="financeRuleItems">
          {group.items.map(([label, text]) => <div className="financeRuleItem" key={label}><b>{label}</b><p>{text}</p></div>)}
        </div>
      </section>)}
    </div>

    <section className="card financeRulesExample">
      <div className="cardHead"><div><span className="eyebrow">WORKED EXAMPLE</span><h2>Same customer value, different tax responsibility</h2><p>The example deliberately excludes delivery because delivery tax treatment is a separate rule.</p></div></div>
      <div className="financeExampleTable">
        <div className="financeExampleHead"><span>Component</span><span>Direct outlet supplier</span><span>ECO Section 9(5)</span></div>
        {EXAMPLE_ROWS.map(row => <div className="financeExampleRow" key={row[0]}><span>{row[0]}</span><b>{row[1]}</b><b>{row[2]}</b></div>)}
      </div>
    </section>
  </section>;
}
