# HealthApp Cloudflare Pages

The customer, outlet, admin and Broccoly React apps are ready to deploy as separate Cloudflare Pages projects from this monorepo.

Cloudflare supports multiple Pages projects from one Git repository with different build commands/output directories. Keep the repository root for the workspace install and use the app-specific build commands below.

## Projects

| Pages project | Root directory | Build command | Build output |
|---|---|---|---|
| healthapp-customer | frontend/healthapp-saas | npm run build:customer | apps/customer-web/dist |
| healthapp-outlet | frontend/healthapp-saas | npm run build:outlet | apps/outlet-web/dist |
| healthapp-admin | frontend/healthapp-saas | npm run build:admin | apps/admin-web/dist |
| broccoly | frontend/healthapp-saas | npm run build:broccoly | apps/broccoly-web/dist |

Standalone test branch: `feature/standalone-saas`

## Cloudflare dashboard setup for standalone testing

Use the existing three Cloudflare projects for the standalone test because marketplace deployments are paused. Do not create a second set of projects.

Configure each existing project as follows:

| Project | Source branch | Custom domain | Build command | Output |
|---|---|---|---|---|
| `healthapp-customer` | `feature/standalone-saas` | `fitfood.broccoly.in` | `npm run build:customer` | `apps/customer-web/dist` |
| `healthapp-outlet` | `feature/standalone-saas` | `outlet.broccoly.in` | `npm run build:outlet` | `apps/outlet-web/dist` |
| `healthapp-admin` | `feature/standalone-saas` | `admin.broccoly.in` | `npm run build:admin` | `apps/admin-web/dist` |
| `broccoly` | `feature/standalone-saas` | `broccoly.in` | `npm run build:broccoly` | `apps/broccoly-web/dist` |

For each project, open Settings -> Builds/Builds & deployments -> Branch control, set the production branch to `feature/standalone-saas`, turn off automatic production branch deployments, and set Preview branch to None. This prevents ordinary feature commits from consuming Cloudflare builds. Cloudflare's documented branch controls support both disabling automatic production deployments and disabling preview deployments. Use the manual GitHub Actions workflow `.github/workflows/cloudflare-standalone-deploy.yml` to deploy a selected app or all three when a cloud test is needed.

Do not add `VITE_API_BASE_URL` as a Cloudflare runtime variable for these static-asset-only Workers/Pages projects. Vite needs this value during `npm run build`.

The manual GitHub Actions deployment workflow `.github/workflows/cloudflare-standalone-deploy.yml` injects the build-time values automatically:

```text
VITE_API_BASE_URL=https://api.broccoly.in/api
VITE_OUTLET_APP_URL=https://app.broccoly.in   # Broccoly build only
```

The workflow then runs the app build and Wrangler deployment. No per-project runtime variable is required for these Vite settings.

Keep the existing custom domains attached to their respective projects.

## API environment variable

The frontend must not use the local `http://localhost:50448/api` value after deployment. The build-time value above is embedded into the Vite bundle during the deployment build.

### Standalone outlet deployment

### Production-style Broccoly domains

Use the platform domain `broccoly.in` for the test environment. The customer site is tenant-aware at runtime, so one shared customer Pages project can serve multiple outlet subdomains without rebuilding the SPA per outlet.

- `fitfood.broccoly.in` -> customer Pages project -> resolves the `fitfood` outlet at runtime
- `outlet.broccoly.in` -> outlet management Pages project
- `admin.broccoly.in` -> Super Admin Pages project
- `api.broccoly.in` -> Azure App Service API

For the first production-style test, do not configure a customer-owned domain. A customer can later attach `www.fitfood.com` as an optional custom domain.

For the standalone customer portal, `VITE_OUTLET_SLUG` is intentionally optional. Production standalone sites resolve the outlet from the browser hostname.

Examples:

- `fitfood.broccoly.in` -> the outlet whose `Subdomain` is `fitfood`
- `www.fitfood.com` -> an optional outlet custom-domain mapping

When a custom domain is requested, Outlet Admin receives a verification token/instructions and Super Admin must verify the DNS change and activate the mapping. Only active mappings are used for tenant resolution.

The customer web app uses the resolved outlet slug for subsequent API requests. The API also checks hostname/header consistency and authenticated `outlet_id` consistency.

Cloudflare Pages does not support a wildcard custom domain such as `*.broccoly.in`. Each standalone outlet hostname (for example `fitfood.broccoly.in`) must therefore be added individually to the shared `healthapp-customer` Pages project. We can automate those per-outlet custom-domain registrations later through the Cloudflare Pages API, while all outlets continue using the same customer Pages deployment, API and SQL Server database.

### Cloudflare Pages API integration

The API can attach requested custom domains to the shared `healthapp-customer` Pages project and read Cloudflare's live validation state. Cloudflare's Pages API exposes the domain status plus `validation_data` including the validation method and TXT name/value when TXT validation is used.

Configure these server-side settings through deployment secrets/environment variables:

- `CloudflarePages:Enabled=true`
- `CloudflarePages:AccountId=<Cloudflare account ID>`
- `CloudflarePages:ProjectName=healthapp-customer`
- `CloudflarePages:ApiToken=<secret API token>`

The API token should have the minimum Pages permissions required for the configured operation. Cloudflare documents `Pages Write` for adding a Pages custom domain and `Pages Read` for reading custom-domain status.

The outlet must still control its DNS. For an apex domain such as `fitfood.com`, Cloudflare's current Pages documentation requires the site to be a Cloudflare zone with its nameservers configured for Cloudflare.

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

For direct-upload deployments after logging in with Wrangler, inject the API URL before the Vite build. On PowerShell:

```powershell
npx wrangler login
$env:VITE_API_BASE_URL='https://api.broccoly.in/api'
$env:VITE_BROCCOLY_URL='https://broccoly.in'
$env:VITE_OUTLET_APP_URL='https://app.broccoly.in'

npm run build:admin
npx wrangler@4 deploy --config apps/admin-web/wrangler.jsonc
```

For the other apps, use the same pattern and run the corresponding build/deploy commands. The GitHub Actions workflow already does this injection automatically and is the preferred repeatable deployment path.

Wrangler project configs are included in each app directory:

- `apps/customer-web/wrangler.jsonc`
- `apps/outlet-web/wrangler.jsonc`
- `apps/admin-web/wrangler.jsonc`
- `apps/broccoly-web/wrangler.jsonc`

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

### 5. Deploy the frontends with the temporary API URL

Set the build-time variable in the deployment command, then build and deploy the selected app. On PowerShell:

```powershell
$env:VITE_API_BASE_URL='https://random-name.trycloudflare.com/api'

npm run build:customer
npx wrangler@4 deploy --config apps/customer-web/wrangler.jsonc
```

Repeat for Outlet and Admin as required. Do not put this in the runtime Variables/Secrets section of a static-asset-only project.

Because Vite embeds `VITE_*` values during the frontend build, a changed API URL requires a new frontend build/deployment.

### Important

This Quick Tunnel is intentionally temporary. It is suitable for testing the HealthApp UI from another device, but it should not be used as the production API architecture. For a stable hostname, create a named Cloudflare Tunnel and map a domain such as `api.example.com` to it. Cloudflare Tunnel is available on all plans, and a named tunnel can be run as a Windows service for persistent connectivity. 
