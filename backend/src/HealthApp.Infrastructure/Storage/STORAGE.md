# HealthApp file and image storage

HealthApp keeps file storage behind the application-layer `IFileStorage` abstraction. Application services and API controllers do not depend on Azure Blob SDK types.

## Providers

- `AzureBlob` — production object/blob storage.
- `Local` — local development fallback.

The provider is selected only in Infrastructure dependency injection:

`Storage:Provider=AzureBlob` or `Storage:Provider=Local`.

To add another provider later (for example an S3-compatible object store), implement `IFileStorage` in Infrastructure and register it for a new provider name. No domain or application code needs to change.

## Production configuration

Set these application settings securely through the deployment environment:

- `Storage:Provider=AzureBlob`
- `Storage:Container=healthapp-media`
- `Storage:ConnectionString` **or** `Storage:AccountUrl`
- `Storage:PublicBaseUrl` when files are served through a custom public hostname/CDN

Files should be stored by logical folders such as `recipes`, `outlets`, `documents`, and `avatars`. Database entities should keep the returned URL/key, not provider-specific SDK objects.

The existing recipe image endpoint already uses `IFileStorage`, so the same abstraction can be reused for outlet images and future document/file uploads.
