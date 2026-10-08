# SaaS Subscription Reactivation — Financial Rule v1.1

**Status:** Production rule  
**Version:** 1.1  
**Effective:** 2026-10-08  
**Supersedes:** v1.0

## Change from v1.0

When an outlet selects a paid SaaS plan while its trial is still active, the paid billing action is treated as an immediate trial conversion. The trial becomes `Converted`, `ConvertedAtUtc` is recorded, the subscription becomes paid `Active`, and the selected billing cycle determines the new renewal date.

This prevents the outlet from retaining an active free-trial state after choosing a paid plan.

## Financial rules

- Monthly plan uses `SaaSPlan.MonthlyFee`.
- Annual plan uses `SaaSPlan.AnnualFee`.
- Existing expired subscriptions are reactivated with `SetupFee = 0`; the onboarding setup fee is not charged again.
- First-time subscriptions may use the configured onboarding setup fee.
- Reactivation/conversion starts the paid billing period on the UTC activation date and sets renewal one month or one year later.
- The selected SaaS plan's `CustomerTransactionFeePercent` becomes the outlet subscription transaction fee percentage.
- Customer package/order taxes and customer transaction charges remain separate calculations and are not retroactively recalculated by SaaS reactivation.

## Lifecycle

| Previous state | Paid billing action | Result |
|---|---|---|
| Active trial | Select paid plan | Trial = Converted; subscription = Active |
| Expired trial/subscription | Select paid plan | Trial = Converted where present; subscription = Active |
| Active paid subscription | Change plan | Subscription remains Active with selected plan |
| Converted trial | Start another free trial | Not allowed |
| No subscription | First paid plan | New Active subscription; onboarding setup fee may apply |

## Audit and versioning

The existing outlet/subscription identity is retained. Super Admin reactivation records the acting user ID and reason. Future financial-rule changes must create a new immutable versioned document.
