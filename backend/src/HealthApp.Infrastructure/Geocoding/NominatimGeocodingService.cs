using System.Text.Json;
using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;

namespace HealthApp.Infrastructure.Geocoding;

public sealed class NominatimGeocodingService(HttpClient client) : IGeocodingService
{
    public async Task<ReverseGeocodeDto?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var query = $"reverse?format=jsonv2&addressdetails=1&zoom=18&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        using var response = await client.GetAsync(query, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("address", out var address))
            return null;

        static string? Get(JsonElement value, string name) =>
            value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        var city = Get(address, "city") ?? Get(address, "town") ?? Get(address, "village") ?? Get(address, "municipality");
        return new ReverseGeocodeDto(
            Get(address, "house_number"),
            Get(address, "road"),
            Get(address, "suburb"),
            Get(address, "neighbourhood") ?? Get(address, "quarter"),
            city,
            Get(address, "state"),
            Get(address, "postcode"),
            Get(address, "country"),
            Get(root, "display_name"));
    }
}
