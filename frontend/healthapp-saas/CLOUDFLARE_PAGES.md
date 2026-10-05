# HealthApp Cloudflare Pages

The customer, outlet and admin React apps are ready to deploy as three separate Cloudflare Pages projects from this monorepo.

Cloudflare supports multiple Pages projects from one Git repository with different build commands/output directories. Keep the repository root for the workspace install and use the app-specific build commands below.

## Projects

| Pages project | Root directory | Build command | Build output |
|---|---|---|---|
| healthapp-customer | frontend/healthapp-saas | npm run build:customer | apps/customer-web/dist |
| healthapp-outlet | frontend/healthapp-saas | npm run build:outlet | apps/outlet-web/dist |
| healthapp-admin | frontend/healthapp-saas | npm run build:admin | apps/admin-web/dist |

Production branch: `main`

## Cloudflare dashboard setup

In Cloudflare:

1. Workers & Pages -> Create application -> Pages -> Import an existing Git repository.
2. Select `pbnaresh-dotnet/Healthe`.
3. Set the production branch to `main`.
4. Set the project Root directory to `frontend/healthapp-saas`.
5. Use the matching build command and output directory from the table.
6. Deploy.

Repeat for all three projects. Cloudflare will give each site a `*.pages.dev` address and can rebuild automatically after commits to the connected repository.

## API environment variable

The frontend must not use the local `http://localhost:50448/api` value after deployment.

In each Pages project go to Settings -> Environment variables and add:

`VITE_API_BASE_URL=https://YOUR-PUBLIC-API-HOST/api`

### Standalone outlet deployment

For a standalone outlet customer portal, `VITE_OUTLET_SLUG` is optional. It remains useful for local development or a fixed per-outlet Pages build, but production standalone sites can resolve the outlet from their runtime hostname.

Examples:

- `fitfood.healthapp.com` -> the outlet whose `Subdomain` is `fitfood`
- `www.fitfood.com` -> the outlet with an active `OutletDomains.Hostname` mapping

When a custom domain is requested, Outlet Admin receives a verification token/instructions and Super Admin must verify the DNS change and activate the mapping. Only active mappings are used for tenant resolution.

The customer web app uses the resolved outlet slug for subsequent API requests. The API also checks hostname/header consistency and authenticated `outlet_id` consistency.

Each standalone outlet can therefore use its own Cloudflare Pages deployment and custom domain while all outlets continue using the same API and SQL Server database.


Set it for Production and Preview as appropriate.

The current ASP.NET Core API already permits cross-origin requests, so the deployed Pages origins do not need a special CORS change for the present test setup.

## Current repository helpers

From `frontend/healthapp-saas`:

```powershell
npm install

npm run build:customer
npm run build:outlet
npm run build:admin
```

For direct-upload deployments after logging in with Wrangler:

```powershell
npx wrangler login

npm run cf:deploy:customer
npm run cf:deploy:outlet
npm run cf:deploy:admin
# or
npm run cf:deploy:all
```

Wrangler project configs are included in each app directory:

- `apps/customer-web/wrangler.jsonc`
- `apps/outlet-web/wrangler.jsonc`
- `apps/admin-web/wrangler.jsonc`

Use the dashboard Git integration for automatic deployments, or Wrangler for manual/direct uploads; do not mix configuration ownership casually.

## Local API made public for testing

Cloudflare Pages can host the frontends, but a browser on the internet cannot call your local `localhost:50448` API. For a temporary test environment, expose the API with a Cloudflare Tunnel and use the resulting HTTPS hostname as `VITE_API_BASE_URL`.

Example flow:

```text
Cloudflare Pages
  ├─ customer
  ├─ outlet
  └─ admin
       |
       v
https://api-test.example.com
       |
Cloudflare Tunnel
       |
localhost:50448
       |
ASP.NET Core + SQL Server
```

Keep the tunnel/API for testing only; do not expose a development SQL Server or unrestricted admin API in a production environment.

## Useful verification

After deployment:

```text
Customer: sign in -> Find Meals -> outlet -> Build Package
Outlet: sign in -> Dashboard -> Routes
Admin: sign in -> Platform dashboard
```

For API verification, open:

`https://YOUR-PUBLIC-API-HOST/health`

and confirm the API returns `status=ok`.


## Temporary public API with Cloudflare Quick Tunnel

For a free test environment, keep the .NET API and SQL Server on the development PC and expose only the HTTP API through a Cloudflare Quick Tunnel.

The API's local HTTP address is:

```text
http://localhost:50448
```

### 1. Install cloudflared on Windows

Download the current Windows 64-bit `cloudflared` executable from Cloudflare's official downloads page:

https://developers.cloudflare.com/tunnel/downloads/

Rename it to `cloudflared.exe` and place it in a folder such as:

```text
C:\Cloudflared\cloudflared.exe
```

Cloudflare's Windows instructions also allow installing it as a service when you need a persistent named tunnel. For the temporary test flow, the standalone executable is enough.

Verify:

```powershell
C:\Cloudflared\cloudflared.exe --version
```

### 2. Start the HealthApp API

From the repository:

```powershell
dotnet run --project backend/src/HealthApp.Api --launch-profile http
```

Confirm locally in a browser:

```text
http://localhost:50448/health
```

You should get a JSON response containing `"status":"ok"`.

### 3. Start a Quick Tunnel

Open a second PowerShell window:

```powershell
C:\Cloudflared\cloudflared.exe tunnel --url http://localhost:50448
```

Cloudflare will print a temporary URL similar to:

```text
https://random-name.trycloudflare.com
```

Anyone who has that URL can reach the API while the tunnel process is running. Cloudflare documents Quick Tunnels as temporary development tunnels with no domain/account requirement. The hostname changes when you create a new Quick Tunnel and the URL stops working when the process exits.

### 4. Test the public API

Open:

```text
https://random-name.trycloudflare.com/health
```

The response should still contain:

```json
{"status":"ok"}
```

Then the customer API is:

```text
https://random-name.trycloudflare.com/api/...
```

### 5. Connect the Customer Worker

In Cloudflare:

```text
Workers & Pages
  -> healthapp-customer
  -> Settings
  -> Variables and Secrets
```

Create/update:

```text
VITE_API_BASE_URL=https://random-name.trycloudflare.com/api
```

Set it for the Production environment.

Because Vite embeds `VITE_*` values during the frontend build, changing this variable requires a new deployment of the Customer Worker.

### 6. Connect Outlet and Admin

Repeat the same variable for:

```text
healthapp-outlet
VITE_API_BASE_URL=https://random-name.trycloudflare.com/api

healthapp-admin
VITE_API_BASE_URL=https://random-name.trycloudflare.com/api
```

Then trigger a new deployment for each Worker.

### Important

This Quick Tunnel is intentionally temporary. It is suitable for testing the HealthApp UI from another device, but it should not be used as the production API architecture. For a stable hostname, create a named Cloudflare Tunnel and map a domain such as `api.example.com` to it. Cloudflare Tunnel is available on all plans, and a named tunnel can be run as a Windows service for persistent connectivity. 
