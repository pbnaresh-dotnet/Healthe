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

        var segments = (folder ?? "files")
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..");
        var safeFolder = string.Join("/", segments);
        var extension = Path.GetExtension(fileName);
        var key = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}".Replace("\\", "/");
        var blob = _container.GetBlobClient(key);
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = resolvedContentType } },
            cancellationToken);

        var publicBase = _options.PublicBaseUrl?.TrimEnd('/');
        var url = string.IsNullOrWhiteSpace(publicBase) ? blob.Uri.ToString() : $"{publicBase}/{key}";
        return new FileStorageResult(url, key, resolvedContentType);
    }
    public async Task<FileStorageResult> UploadPrivateAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var segments = (folder ?? "private")
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..");
        var safeFolder = string.Join("/", segments);
        var extension = Path.GetExtension(fileName);
        var key = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}".Replace("\\", "/");
        var blob = _container.GetBlobClient(key);
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = resolvedContentType } },
            cancellationToken);

        // Private objects are never returned as public URLs. The API streams them after authorization.
        return new FileStorageResult(string.Empty, key, resolvedContentType);
    }

    public async Task<FileStorageDownload?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var blob = _container.GetBlobClient(key);
        if (!await blob.ExistsAsync(cancellationToken))
            return null;

        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new FileStorageDownload(response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream");
    }

}
