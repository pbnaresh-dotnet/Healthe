# HealthApp - SQL Server + EF Core 10.0.12

HealthApp is a database-backed meal subscription SaaS application for customers, outlet admins and super admins.

## Stack

- .NET 10
- EF Core / SQL Server 10.0.12
- SQL Server 2022 (Docker supported)
- React + Vite
- JWT authentication
- Repository + Unit of Work
- Strategy / Factory / Domain Event patterns

## Run SQL Server

```powershell
docker compose -f docker-compose.sqlserver.yml up -d
```

Or point `backend/src/HealthApp.Api/appsettings.json` at an existing SQL Server instance.

## Run API

```powershell
cd backend
dotnet restore
dotnet build HealthApp.sln
dotnet run --project src/HealthApp.Api
```

The demo startup initializes the EF Core model with `EnsureCreatedAsync()` and seeds demo data. For production, generate EF Core migrations and switch the initializer to `MigrateAsync()`.

API: `http://localhost:50448/api`
Swagger: `https://localhost:50447/swagger`

## Run React

```powershell
cd frontend/healthapp-saas
copy .env.example .env
npm install
npm run dev:customer
```

Other apps: `npm run dev:outlet` and `npm run dev:admin`.

## Demo users

- Customer: `customer@healthapp.test` / `demo`
- Outlet Admin: `admin@fitfood.test` / `demo`
- In-house Driver: `driver@fitfood.test` / `demo` (fresh database seed)
- Super Admin: `admin@healthapp.test` / `demo`

## Delivery routing

Outlet Admins can select a delivery date, view eligible delivery points on a map, select active in-house drivers, and generate road-optimized driver routes. Deliveries sharing the same address are grouped into one route stop, and each delivery is persisted with its assigned route and stop sequence.

The route adapter defaults to OSRM. Set `Routing:OsrmBaseUrl` to another compatible routing service or replace the adapter for a production routing provider.

## Main business capabilities

### Customer

- Weight, height, BMI, goal, activity level, allergies and diet
- Outlet discovery and service-radius checking
- Weekly / bi-weekly / monthly custom meal packages
- Day x meal-slot selections and portions
- Weekly pattern repeat support in the data model/client flow
- Multiple delivery addresses and master-area selection
- Individual-meal delivery or one-delivery-per-day
- Configurable distance-based delivery pricing
- Configurable duration + committed meal discount tiers
- Discount codes
- Quote + payment idempotency architecture
- Free/late skip with ₹50 late-skip HealthApp fee
- Unused meal lifecycle and rescheduling within subscription end + 7 days
- Meal selection audit history

### Outlet

- Recipes and nutrition
- Weekly menu by day and meal slot
- Customer/subscription/order/delivery views
- SaaS plan and setup fee
- Serviceable master areas
- Configurable delivery slabs
- Configurable discount tiers and codes
- Delivery labels

### Super Admin

- Outlet/user dashboard
- Revenue reporting
- City/area master management

## Financial model

The model stores gross meal value, package discounts, discount-code discounts, net meal value, restaurant GST, delivery, HealthApp service fee, service-fee GST, outlet commission, late-skip fees, customer payable, outlet settlement and HealthApp revenue.

Default configuration is 3% HealthApp service fee, 5% restaurant GST and 18% GST on the HealthApp service fee. Validate the final tax and marketplace/ECO treatment with the appropriate Indian GST professional before production.

## Security / secrets

No local `.env` or real password is committed. Copy `frontend/healthapp-saas/.env.example` to `.env` for local frontend settings. Replace JWT and SQL placeholders with local secrets.

## Note on build verification

The assembly environment used for this repository did not have the .NET SDK installed, and external npm registry access was unavailable. The source was inspected and the reported compile errors were fixed in code, but a local `dotnet build` / `npm install` run is still required before deployment.
