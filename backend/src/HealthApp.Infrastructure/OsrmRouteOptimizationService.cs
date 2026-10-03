using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure;

public sealed class OsrmRouteOptimizationService(HttpClient http) : IRouteOptimizationService
{
    public async Task<RouteOptimizationResult> OptimizeAsync(
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

        var uri = $"/trip/v1/driving/{coordinates}?source=first&destination=any&roundtrip=false&steps=false&geometries=geojson&overview=full";

        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OsrmTripResponse>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Routing provider returned an empty response.");

        if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase) || payload.Trips.Count == 0)
            throw new InvalidOperationException($"Routing provider returned '{payload.Code ?? "unknown"}'.");

        var trip = payload.Trips[0];

        var ordered = payload.Waypoints
            .Select((waypoint, inputIndex) => (waypoint, inputIndex))
            .Where(x => x.inputIndex > 0)
            .OrderBy(x => x.waypoint.WaypointIndex)
            .Select(x => x.inputIndex - 1)
            .Where(x => x >= 0 && x < stops.Count)
            .Select(x => stops[x].Id)
            .ToList();

        if (ordered.Count != stops.Count)
            throw new InvalidOperationException("Routing provider did not return every delivery stop.");

        var geometry = trip.Geometry?.Coordinates?
            .Select(x => (IReadOnlyList<double>)new[] { x[0], x[1] })
            .ToList()
            ?? [new[] { outletLongitude, outletLatitude }];

        return new(
            trip.Distance / 1000d,
            trip.Duration / 60d,
            ordered,
            geometry);
    }

    private sealed class OsrmTripResponse
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("waypoints")] public List<OsrmWaypoint> Waypoints { get; set; } = [];
        [JsonPropertyName("trips")] public List<OsrmTrip> Trips { get; set; } = [];
    }

    private sealed class OsrmWaypoint
    {
        [JsonPropertyName("waypoint_index")] public int WaypointIndex { get; set; }
    }

    private sealed class OsrmTrip
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
