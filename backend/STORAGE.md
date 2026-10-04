# HealthApp file storage

All image and file uploads go through the `IFileStorage` application abstraction. API/application code does not depend on Azure Blob SDKs.

## Development

The default provider is `Local`. Files are stored under `backend/src/HealthApp.Api/wwwroot/uploads` and are served by ASP.NET Core static files.

## Production

Use `Storage:Provider=AzureBlob`. Azure authentication supports either:

- `Storage:ConnectionString` for a storage account connection string.
- `Storage:AccountUrl` with the app's Azure managed identity / `DefaultAzureCredential`.

For recipe/menu images, set `Storage:PublicBaseUrl` to the public blob/CDN base URL so stored image URLs are usable by the customer and outlet portals. Keep credentials in deployment secrets/environment variables rather than source control.

To add another provider later, implement `IFileStorage` and register it from `DependencyInjection.cs`; no controller or application-service changes are required.
