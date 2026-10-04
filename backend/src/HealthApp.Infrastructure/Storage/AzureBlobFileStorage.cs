using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Storage;

public sealed class AzureBlobFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly StorageOptions _options = options.Value;
    private readonly BlobContainerClient _container = CreateContainer(options.Value);

    private static BlobContainerClient CreateContainer(StorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            return new BlobContainerClient(options.ConnectionString, options.Container);

        if (!string.IsNullOrWhiteSpace(options.AccountUrl))
        {
            var service = new BlobServiceClient(new Uri(options.AccountUrl), new DefaultAzureCredential());
            return service.GetBlobContainerClient(options.Container);
        }

        throw new InvalidOperationException("Storage:ConnectionString or Storage:AccountUrl is required when Storage:Provider is AzureBlob.");
    }

    public async Task<FileStorageResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var safeFolder = string.Join("/", (folder ?? "files").Split('/', '\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var extension = Path.GetExtension(fileName);
        var key = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}".Replace("\", "/");
        var blob = _container.GetBlobClient(key);

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType } },
            cancellationToken);

        var publicBase = _options.PublicBaseUrl?.TrimEnd('/');
        var url = string.IsNullOrWhiteSpace(publicBase) ? blob.Uri.ToString() : $"{publicBase}/{key}";
        return new FileStorageResult(url, key, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
    }
}
