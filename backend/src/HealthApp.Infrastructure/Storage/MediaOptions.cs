namespace HealthApp.Infrastructure.Storage;

public sealed class MediaOptions
{
    public long MaxImageUploadBytes { get; set; } = 10_000_000;
    public int MaxImageDimension { get; set; } = 5000;
    public int WebpQuality { get; set; } = 82;
    public int ThumbnailQuality { get; set; } = 76;
}
