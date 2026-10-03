using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure;

public sealed class OsrmRouteOptimizationService(HttpClient http) : IRouteOptimizationService
{
    public async Task<RouteOptimizationResult> RouteInOrderAsync(
        double outletLatitude,
        double outletLongitude,
        IReadOnlyList<RouteOptimizationStop> stops,
        CancellationToken cancellationToken = default)
    {
        if (stops.Count == 0)
            return new(0d, 0d, [], [new[] { outletLongitude, outletLatitude }]);

        static string Coordinate(double longitude, double latitude)
            => $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}";

        var coordinates = string.Join(
            ';',
            new[] { Coordinate(outletLongitude, outletLatitude) }
                .Concat(stops.Select(x => Coordinate(x.Longitude, x.Latitude))));

        var uri = $"/route/v1/driving/{coordinates}?steps=false&geometries=geojson&overview=full";

        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OsrmRouteResponse>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Routing provider returned an empty response.");

        if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase) || payload.Routes.Count == 0)
            throw new InvalidOperationException($"Routing provider returned '{payload.Code ?? "unknown"}'.");

        var route = payload.Routes[0];
        var geometry = route.Geometry?.Coordinates
            ?.Where(x => x.Length >= 2)
            .Select(x => (IReadOnlyList<double>)new[] { x[0], x[1] })
            .ToList();

        if (geometry is null || geometry.Count == 0)
            geometry = [new[] { outletLongitude, outletLatitude }];

        return new(
            route.Distance / 1000d,
            route.Duration / 60d,
            stops.Select(x => x.Id).ToList(),
            geometry);
    }

    private sealed class OsrmRouteResponse
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("routes")] public List<OsrmRoute> Routes { get; set; } = [];
    }

    private sealed class OsrmRoute
    {
        [JsonPropertyName("distance")] public double Distance { get; set; }
        [JsonPropertyName("duration")] public double Duration { get; set; }
        [JsonPropertyName("geometry")] public OsrmGeometry? Geometry { get; set; }
    }

    private sealed class OsrmGeometry
    {
        [JsonPropertyName("coordinates")] public List<double[]> Coordinates { get; set; } = [];
    }
}
