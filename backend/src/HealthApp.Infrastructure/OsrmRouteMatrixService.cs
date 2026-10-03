using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure;

public sealed class OsrmRouteMatrixService(HttpClient http) : IRouteMatrixService
{
    private const long Unreachable = 1_000_000_000L;

    public async Task<RouteTravelMatrix> BuildAsync(
        IReadOnlyList<RouteOptimizationStop> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0)
            return new(new long[0, 0], new double[0, 0]);

        static string Coordinate(double longitude, double latitude)
            => $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}";

        var coordinates = string.Join(';', points.Select(x => Coordinate(x.Longitude, x.Latitude)));
        var uri = $"/table/v1/driving/{coordinates}?annotations=duration,distance";

        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OsrmTableResponse>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Routing matrix provider returned an empty response.");

        if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Routing matrix provider returned '{payload.Code ?? "unknown"}'.");

        if (payload.Durations.Count != points.Count || payload.Distances.Count != points.Count)
            throw new InvalidOperationException("Routing matrix provider returned an invalid matrix size.");

        var duration = new long[points.Count, points.Count];
        var distance = new double[points.Count, points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            if (payload.Durations[i].Count != points.Count || payload.Distances[i].Count != points.Count)
                throw new InvalidOperationException("Routing matrix provider returned an incomplete matrix row.");

            for (var j = 0; j < points.Count; j++)
            {
                var seconds = payload.Durations[i][j];
                var meters = payload.Distances[i][j];

                duration[i, j] = seconds.HasValue
                    ? Math.Max(0L, (long)Math.Round(seconds.Value))
                    : (i == j ? 0L : Unreachable);
                distance[i, j] = meters ?? (i == j ? 0d : Unreachable);
            }
        }

        return new(duration, distance);
    }

    private sealed class OsrmTableResponse
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("durations")] public List<List<double?>> Durations { get; set; } = [];
        [JsonPropertyName("distances")] public List<List<double?>> Distances { get; set; } = [];
    }
}
