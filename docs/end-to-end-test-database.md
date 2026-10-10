# Fresh end-to-end test database

This setup is intended for a **disposable test database only**. It does not delete or reset any existing database.

## What gets seeded

On first API startup against an empty database, EF Core creates the schema and the initializer seeds platform/master data, including:

- SaaS subscription plans (Free, Basic, Growth, Professional)
- Ingredient and allergen catalog with nutrition reference data and ingredient/allergen links
- Service-city and delivery-area reference data for supported Indian cities
- Platform finance defaults and ledger/reference data
- A Super Admin account so the outlet registration/approval lifecycle can be tested

It deliberately does **not** create demo outlets, outlet admins, customers, sample recipes, meal plans, subscriptions, or orders. Create those through the actual UI during the test.

## Local SQL Server

1. Ensure local SQL Server is running and the configured Windows account can create databases.
2. Start the API with the `EndToEndTest` environment:
   `$env:ASPNETCORE_ENVIRONMENT = "EndToEndTest"`
3. Run the API normally. The configured database name is `BroccolyE2E`; the startup initializer creates the schema and seeds the master data.
4. Open the Super Admin portal and sign in using the test-only credentials below.

The config file `appsettings.EndToEndTest.json` uses Windows authentication. If your local SQL Server uses SQL authentication or a different instance, override `ConnectionStrings__DefaultConnection` in your shell or user secrets.

## Azure SQL test database

Create a **new, separate Azure SQL database** (for example, `BroccolyE2E`) on the existing server. Do not point this environment at the current shared/production database.

Set these Application Settings on the **test API App Service only**:

- `ASPNETCORE_ENVIRONMENT` = `EndToEndTest`
- `ConnectionStrings__DefaultConnection` = the connection string for the new `BroccolyE2E` database
- `Database__SeedDemoData` = `false`

Store credentials in App Service settings/Key Vault, not in source control. The environment-specific JSON file intentionally contains no Azure credentials.

## Test-only Super Admin

- Email: `admin@healthapp.test`
- Password: `demo`

Change this password immediately if the test database is shared with anyone. Never use these credentials or this seed mode for production.

## Resetting the test database

To start over, back up anything you need, then drop/recreate **only the dedicated `BroccolyE2E` test database** and restart the API. Do not run a destructive reset against `HealthAppDb` or any production/shared database.
