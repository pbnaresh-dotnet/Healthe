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
}
