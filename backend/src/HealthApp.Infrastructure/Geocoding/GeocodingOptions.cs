namespace HealthApp.Infrastructure.Geocoding;

public sealed class GeocodingOptions
{
    public string Provider { get; set; } = "Nominatim";
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";
    public string UserAgent { get; set; } = "HealthApp/1.0";
    public string Referer { get; set; } = "";
}
