# Clean SaaS database and Area Manager workflow

## One-time clean database reset

The API supports a **destructive, one-time** reset for an existing SQL Server database. It is opt-in and must be explicitly enabled in the API configuration.

Set this app setting for one API startup:

```text
Database__ResetTenantDataOnStartup=true
```

In Azure App Service, add it under **Configuration → Application settings**, save and restart the API. The initializer records a `CleanSaaSDatabaseV1` maintenance marker so the purge is not repeated on later starts, even if the setting is accidentally left enabled.

The reset deletes outlet/tenant operational data, outlet users, subscriptions, orders, deliveries, menus, recipes, onboarding records, transactions, finance documents, and related records. It preserves master/catalog data (`Ingredients`, `Allergens`, `ServiceCities`, `CityAreas`, `SaaSPlans`, `OutletGroups`) and the platform Super Admin user `admin@healthapp.test` only. The normal clean seed path then runs with demo data disabled.

**Back up the database before enabling this setting.** Do not enable it against a database containing real customer or financial records unless that data is intentionally being permanently removed. After the reset succeeds, remove the app setting. The reset code has not been run against the hosted database by this repository change; an API restart with the setting enabled is required.

The clean seed Super Admin credentials are currently `admin@healthapp.test` / `demo` where that user does not already exist. Change this password immediately and replace demo credentials with a secure deployment-specific bootstrap secret before production use.

## Area Manager

Area Manager is a platform-level role, separate from outlet staff roles. Super Admin can create an Area Manager account and assign one or more outlets to it. The assignments are stored in `AreaManagerOutletAssignments`; the API validates that each assigned outlet exists and prevents duplicate manager/outlet assignments.

Super Admin endpoints (require a Super Admin bearer token):

- `GET /api/admin/area-managers` — list Area Managers and their assigned outlet IDs/names.
- `POST /api/admin/area-managers` — create an Area Manager and assign outlets. Request body:

```json
{
  "email": "manager@example.com",
  "password": "Use-a-long-unique-password",
  "firstName": "Area",
  "lastName": "Manager",
  "mobileNumber": "+91XXXXXXXXXX",
  "outletIds": ["00000000-0000-0000-0000-000000000000"]
}
```

- `PUT /api/admin/area-managers/{userId}/outlets` — replace the manager's assigned outlets.
- `PATCH /api/admin/area-managers/{userId}/status` — activate/deactivate an Area Manager.

Area Manager workspace endpoints (Area Manager bearer token):

- `GET /api/area-manager/dashboard` — counts only for assigned outlets.
- `GET /api/area-manager/outlets` — only assigned outlets.

The embedded Super Admin UI at `broccoly.in/admin` now includes an **Area Managers** page to create accounts, select assigned outlets, edit assignments, and activate/deactivate managers. Area Manager accounts sign in to the same embedded console and see only their assigned-outlet workspace with status counts and assigned outlet/location information. The current workspace is intentionally read-only: it does not expose Super Admin actions or outlet operational write APIs. Additional actions (such as handling outlet requests or updating outlet configuration) should be introduced as explicitly scoped endpoints with an audit trail.


## Super Admin: create outlet with SaaS subscription

The embedded Super Admin console at `broccoly.in/admin` now has an **Add outlet and subscription** form inside **Tenants → Outlets**. It creates the outlet, an active Outlet Admin login and the selected SaaS subscription together in one database transaction.

Super Admin endpoints (require a Super Admin bearer token):

- `GET /api/admin/saas-plans` — returns active, paid SaaS plans for the form.
- `POST /api/admin/outlets` — provisions the outlet and owner account and assigns the chosen plan.

Required request fields: `outletName`, `ownerFirstName`, `ownerLastName`, `email`, `password`, `city`, `state`, `pincode`, `saasPlanId`, and `billingCycle`. Optional `mobileNumber`. Billing cycles accepted are `Monthly`, `SixMonths` and `Annual`. Six-month pricing uses the existing 10% discount calculation (monthly fee × 6 × 90%); annual pricing uses the plan's configured annual fee. Setup fee reads `Onboarding:SetupFee` and defaults to ₹5,000 if not configured. The plan's customer transaction fee percentage is copied to the outlet subscription.

This is an **admin provisioning action, not a payment collection**: the endpoint does not call Cashfree or create a payment transaction. The form discloses this, and the response labels payment status as `NotCollected`. Super Admin should reconcile setup/subscription payment separately before treating this as a paid financial transaction. It also does not collect or publish outlet-specific customer legal policies; those remain an outlet onboarding/configuration task.

Outlet slug/subdomain is generated from the outlet name, with collision suffixes and reserved-name protection. The API rejects duplicate owner email addresses and creates the outlet, owner and subscription atomically. These changes have not been build- or integration-tested and are not deployed yet.


## Discounts and manual payment for Super Admin outlet provisioning

The Add outlet form supports a percentage discount from 0% to 100% applied to the combined one-time setup fee and selected subscription amount. The API validates the percentage and stores both the rate and calculated amount on `OutletSubscription`; the payment record's immutable detail JSON captures gross amount, discount, net amount, plan, cycle, payment method, reference, notes and the Super Admin actor.

Super Admin may leave payment pending or select **Mark amount as paid** and record Cash, UPI, bank transfer, Cashfree or Other. Cash receipt reference is optional; non-cash references are required. Marking paid creates a `PaymentTransaction` with a paid status and timestamp. This is an administrative receipt record, not proof from Cashfree for online payments; the operator must reconcile external payments before using that option. The API never marks the amount paid just because an outlet is created.


For outlets created without marking payment received, the outlet and owner account remain pending/inactive. Once payment is actually received, `POST /api/admin/outlets/{outletId}/mark-paid` records the manual payment and activates the subscription, outlet and owner login. This endpoint is available for backend integration; the current embedded UI supports marking paid during creation, but does not yet expose a separate follow-up payment action in the outlet directory.
