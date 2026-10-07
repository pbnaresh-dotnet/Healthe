using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Storage;

public sealed class LocalFileStorage(IHostEnvironment environment, IOptions<StorageOptions> options) : IFileStorage
{
    public async Task<FileStorageResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        var segments = (folder ?? "files")
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..");
        var safeFolder = string.Join("/", segments);
        var extension = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var root = Path.IsPathRooted(options.Value.LocalRoot)
            ? options.Value.LocalRoot
            : Path.Combine(environment.ContentRootPath, options.Value.LocalRoot);
        var directory = Path.Combine(root, safeFolder.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Directory.CreateDirectory(directory);

        var physicalPath = Path.Combine(directory, storedName);
        await using var output = System.IO.File.Create(physicalPath);
        await content.CopyToAsync(output, cancellationToken);

        var key = $"{safeFolder}/{storedName}".Replace("\\", "/");
        var publicBase = string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl) ? "/uploads" : options.Value.PublicBaseUrl.TrimEnd('/');
        return new FileStorageResult($"{publicBase}/{key}", key, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
    }
    public async Task<FileStorageResult> UploadPrivateAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        var segments = (folder ?? "private")
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..");
        var safeFolder = string.Join("/", segments);
        var extension = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var root = Path.IsPathRooted(options.Value.PrivateLocalRoot)
            ? options.Value.PrivateLocalRoot
            : Path.Combine(environment.ContentRootPath, options.Value.PrivateLocalRoot);
        var directory = Path.Combine(root, safeFolder.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Directory.CreateDirectory(directory);

        var physicalPath = Path.Combine(directory, storedName);
        await using var output = System.IO.File.Create(physicalPath);
        await content.CopyToAsync(output, cancellationToken);

        var key = $"{safeFolder}/{storedName}".Replace("\\", "/");
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        return new FileStorageResult(string.Empty, key, resolvedContentType);
    }

    public async Task<FileStorageDownload?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var root = Path.GetFullPath(Path.IsPathRooted(options.Value.PrivateLocalRoot)
            ? options.Value.PrivateLocalRoot
            : Path.Combine(environment.ContentRootPath, options.Value.PrivateLocalRoot));
        var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (relative.Contains("..", StringComparison.Ordinal))
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!File.Exists(fullPath))
            return null;

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        var contentType = "application/octet-stream";
        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) contentType = "application/pdf";
        else if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) contentType = "image/jpeg";
        else if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)) contentType = "image/png";
        else if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)) contentType = "image/webp";
        else if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)) contentType = "application/msword";
        else if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)) contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        else if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)) contentType = "application/vnd.ms-excel";
        else if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return new FileStorageDownload(stream, contentType);
    }

}
