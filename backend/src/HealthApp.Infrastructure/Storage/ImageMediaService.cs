using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace HealthApp.Infrastructure.Storage;

public sealed class ImageMediaService(
    IFileStorage storage,
    IOptions<MediaOptions> options) : IMediaService
{
    private readonly MediaOptions _options = options.Value;

    public async Task<MediaUploadResult> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        MediaImageProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (content is null || !content.CanRead)
            throw new ArgumentException("The uploaded image could not be read.");

        var input = await CopyWithLimitAsync(content, _options.MaxImageUploadBytes, cancellationToken);
        using var image = Image.Load(input);

        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("The uploaded image has invalid dimensions.");

        if (image.Width > _options.MaxImageDimension || image.Height > _options.MaxImageDimension)
            throw new ArgumentException($"Image dimensions must be {_options.MaxImageDimension}x{_options.MaxImageDimension} or smaller.");

        var definition = ProfileDefinition.For(profile);
        var baseFolder = SanitizeFolder(folder);
        var assetId = Guid.NewGuid().ToString("N");
        var variants = new Dictionary<string, MediaVariantDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var variant in definition.Variants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var variantImage = image.Clone();
            ApplyResize(variantImage, definition.Mode, variant.Width, variant.Height);

            await using var output = new MemoryStream();
            await variantImage.SaveAsync(
                output,
                new WebpEncoder
                {
                    Quality = variant.IsThumbnail ? _options.ThumbnailQuality : _options.WebpQuality
                },
                cancellationToken);

            output.Position = 0;
            var stored = await storage.UploadAsync(
                output,
                $"{assetId}.webp",
                "image/webp",
                $"{baseFolder}/{assetId}/{variant.Name}",
                cancellationToken);

            variants[variant.Name] = new MediaVariantDto(
                variant.Name,
                stored.Url,
                stored.Key,
                stored.ContentType,
                output.Length,
                variant.Width,
                variant.Height);
        }

        var preferred = definition.PreferredName;
        var preferredVariant = variants[preferred];

        return new MediaUploadResult(
            preferredVariant.Url,
            preferredVariant.Key,
            preferredVariant.ContentType,
            input.Length,
            image.Width,
            image.Height,
            new ReadOnlyDictionary<string, MediaVariantDto>(variants));
    }

    private static void ApplyResize(
        Image image,
        ResizeMode mode,
        int width,
        int height)
    {
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = mode,
            Position = AnchorPositionMode.Center
        }));
    }

    private async Task<MemoryStream> CopyWithLimitAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var result = new MemoryStream();
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new ArgumentException($"Image must be {_options.MaxImageUploadBytes / 1_000_000} MB or smaller.");

            await result.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        result.Position = 0;
        return result;
    }

    private static string SanitizeFolder(string? folder)
    {
        var segments = (folder ?? "images")
            .Split(new[] { '/', '\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x != "." && x != "..")
            .Select(x => string.Concat(x.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')));

        var safe = string.Join("/", segments.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(safe) ? "images" : safe;
    }

    private sealed record VariantDefinition(
        string Name,
        int Width,
        int Height,
        bool IsThumbnail);

    private sealed record ProfileDefinition(
        ResizeMode Mode,
        string PreferredName,
        IReadOnlyList<VariantDefinition> Variants)
    {
        public static ProfileDefinition For(MediaImageProfile profile) => profile switch
        {
            MediaImageProfile.Recipe => new(
                ResizeMode.Crop,
                "medium",
                [
                    new("thumbnail", 160, 120, true),
                    new("small", 400, 300, false),
                    new("medium", 800, 600, false),
                    new("large", 1200, 900, false)
                ]),
            MediaImageProfile.OutletHero => new(
                ResizeMode.Crop,
                "large",
                [
                    new("small", 800, 450, false),
                    new("medium", 1200, 675, false),
                    new("large", 1600, 900, false)
                ]),
            MediaImageProfile.OutletLogo => new(
                ResizeMode.Max,
                "medium",
                [
                    new("thumbnail", 64, 64, true),
                    new("small", 128, 128, false),
                    new("medium", 256, 256, false),
                    new("large", 512, 512, false)
                ]),
            MediaImageProfile.Favicon => new(
                ResizeMode.Crop,
                "small",
                [
                    new("thumbnail", 32, 32, true),
                    new("small", 64, 64, false),
                    new("medium", 128, 128, false)
                ]),
            MediaImageProfile.Avatar => new(
                ResizeMode.Crop,
                "medium",
                [
                    new("thumbnail", 64, 64, true),
                    new("small", 128, 128, false),
                    new("medium", 512, 512, false)
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }
}
