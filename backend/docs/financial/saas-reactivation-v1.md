# SaaS Subscription Reactivation — Financial Rule v1.0

**Status:** Production rule  
**Version:** 1.0  
**Effective:** 2026-10-08  
**Scope:** Standalone SaaS outlet subscriptions and trials

## Purpose
Document the financial and lifecycle rules used when an outlet's SaaS trial or paid subscription has expired and the outlet is reactivated.

## Rules
1. **Existing outlet data is retained.** Reactivation does not create a new outlet, customer set, or subscription identity when an existing OutletSubscription exists.
2. **No second free trial.** An expired trial is never restarted by the normal paid reactivation path. The existing trial is marked `Converted` when paid reactivation succeeds.
3. **Paid subscription fee.** The selected active SaaS plan determines the recurring fee: Monthly → SaaSPlan.MonthlyFee; Annual → SaaSPlan.AnnualFee.
4. **Renewal date.** Reactivation starts on the UTC date of successful activation and renews one month or one year later according to the selected billing cycle.
5. **Transaction fee.** The selected SaaS plan's CustomerTransactionFeePercent becomes the outlet subscription transaction fee percentage.
6. **Setup fee.** A first-time subscription may use the configured onboarding setup fee. Reactivation of an existing expired subscription does **not** charge the original setup fee again; SetupFee = 0.
7. **Status transition.** Successful reactivation changes the outlet SaaS subscription to `Active` and the outlet to `Live`.
8. **Trial history.** If an existing trial is `Expired`, successful paid reactivation changes its status to `Converted` and records `ConvertedAtUtc`. The original StartedAtUtc and EndsAtUtc remain unchanged.
9. **Super Admin reactivation.** Super Admin can select an active SaaS plan and billing cycle and reactivate an expired outlet directly. The same fee and setup-fee rules apply.
10. **Billing visibility.** Expired subscriptions remain visible in outlet billing so the outlet can choose a paid plan to reactivate instead of receiving a blank billing page.

## Financial calculation
SubscriptionFee = selected cycle fee from SaaSPlan.
SetupFee = 0 when reactivating an existing expired subscription.
RenewalDate = ReactivationDate + selected billing period.
TransactionFeePercent = SaaSPlan.CustomerTransactionFeePercent.
Customer transaction fees and customer package/order taxes are separate calculations and are not retroactively recomputed by SaaS subscription reactivation.

## Audit expectations
Every reactivation path records the lifecycle state transition in the subscription/trial records. Super Admin reactivation additionally records the acting Super Admin user ID and supplied reason in the reactivation response/audit context.

## Versioning
Future changes to these financial rules must create a new versioned document rather than silently changing this rule set.