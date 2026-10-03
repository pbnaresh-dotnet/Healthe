# HealthApp Architecture

## Persistence

`CustomerService` -> repository abstractions -> EF repositories -> `HealthAppDbContext` -> SQL Server.

Transactions use EF Core execution strategies and explicit SQL transactions.

## Patterns

- Strategy: package discounts, meal pricing, platform fee, tax, delivery mode, late-skip policy
- Factory: delivery-mode strategy factory
- Domain events: subscription creation, meal skip, meal reschedule
- Event handler: idempotent late-skip revenue recording
- Unit of Work: subscription creation and schedule changes
- Repository: SQL/EF persistence boundary
- Idempotency: late-skip event references and payment idempotency keys
- Audit history: meal selection history

## Delivery

Super Admin owns master `CityArea` records.
Outlet Admin selects `OutletDeliveryArea` records and configures `DeliveryPricingRule` slabs.
Customer addresses point to a master area and store coordinates for distance calculation.

Delivery modes:

1. `IndividualMealDelivery` - every meal can have its own delivery.
2. `OneDeliveryPerDay` - meals on the same day share one address and one delivery fee.

## Meal lifecycle

`Scheduled -> Unused -> Rescheduled -> Scheduled` or `Delivered`.

Late skip is a separate HealthApp financial event and is not part of outlet commission.

Unused meals can be rescheduled through subscription end + seven calendar days.
