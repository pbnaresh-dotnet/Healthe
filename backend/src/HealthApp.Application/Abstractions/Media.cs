using System.Collections.ObjectModel;

namespace HealthApp.Application.Abstractions;

public enum MediaImageProfile
{
    Recipe,
    OutletHero,
    OutletLogo,
    Favicon,
    Avatar
}

public sealed record MediaVariantDto(
    string Name,
    string Url,
    string Key,
    string ContentType,
    long SizeBytes,
    int Width,
    int Height);

public sealed record MediaUploadResult(
    string Url,
    string Key,
    string ContentType,
    long OriginalSizeBytes,
    int OriginalWidth,
    int OriginalHeight,
    IReadOnlyDictionary<string, MediaVariantDto> Variants)
{
    public MediaVariantDto? Thumbnail =>
        Variants.TryGetValue("thumbnail", out var value) ? value : null;

    public MediaVariantDto? Small =>
        Variants.TryGetValue("small", out var value) ? value : null;

    public MediaVariantDto? Medium =>
        Variants.TryGetValue("medium", out var value) ? value : null;

    public MediaVariantDto? Large =>
        Variants.TryGetValue("large", out var value) ? value : null;
}

public interface IMediaService
{
    Task<MediaUploadResult> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        MediaImageProfile profile,
        CancellationToken cancellationToken = default);
}
