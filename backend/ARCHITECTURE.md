# HealthApp Architecture

## Persistence

`CustomerService` -> repository abstractions -> EF repositories -> `HealthAppDbContext` -> SQL Server.

## Multi-tenant SaaS

The platform uses a shared SQL Server database with outlet-level tenancy. Tenant-owned business records carry an `OutletId`; customer identities also carry `OutletId`.

Examples:

`Outlet 1 -> Customer A/B/C`
`Outlet 2 -> Customer D/E/F`

Tenant isolation is enforced in layers:

- authenticated customer/outlet tokens carry `outlet_id`;
- `TenantContextMiddleware` resolves the optional `X-Outlet-Slug` request tenant and rejects mismatches;
- outlet application services validate `CurrentUser.OutletId`;
- tenant-sensitive repositories expose outlet-scoped operations where an ID-only operation could cross a tenant boundary;
- SQL Server foreign keys connect tenant-owned tables to `Outlets`;
- customer email uniqueness is scoped to outlet for tenant accounts.

Marketplace APIs remain multi-outlet when no tenant is supplied. Standalone customer deployments set `VITE_OUTLET_SLUG`, which scopes their public discovery and authenticated requests to one outlet.


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


## External service boundaries

Images and other uploaded files use the `IFileStorage` abstraction. The default development provider stores files locally; production can switch to Azure Blob Storage through `Storage:Provider` configuration without changing the API/application layer.

Address map lookup uses the `IGeocodingService` abstraction. Customer-selected latitude/longitude remains the authoritative delivery pinpoint; reverse geocoding only supplies editable address text and helps match the HealthApp city/area master.
