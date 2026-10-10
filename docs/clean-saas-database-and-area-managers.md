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

The role and API foundation are in place. The existing Super Admin UI still needs an Area Managers screen to create users and maintain outlet assignments; the endpoints can be exercised through Swagger/API until that UI is added. Outlet operational write permissions are intentionally not granted implicitly by this role; specific outlet actions should be enabled only after the required contact/approval workflow is agreed and implemented.
