using System.Net;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure;

public sealed class CloudflarePagesService(
    HttpClient http,
    IOptions<CloudflarePagesSettings> options) : ICloudflarePagesService
{
    private readonly CloudflarePagesSettings settings = options.Value;

    public bool IsEnabled => settings.Enabled;

    public async Task<CloudflarePagesDomainState> EnsureDomainAsync(string hostname, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var existing = await GetDomainAsync(hostname, cancellationToken);
        if (existing is not null)
            return existing;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/accounts/{Uri.EscapeDataString(settings.AccountId)}/pages/projects/{Uri.EscapeDataString(settings.ProjectName)}/domains");

        request.Content = new StringContent(
            JsonSerializer.Serialize(new { name = hostname }),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        using var payload = await ReadResponseAsync(response, cancellationToken);
        return ParseDomain(payload, hostname);
    }

    public async Task<CloudflarePagesDomainState?> RetryValidationAsync(string hostname, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return null;

        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/accounts/{Uri.EscapeDataString(settings.AccountId)}/pages/projects/{Uri.EscapeDataString(settings.ProjectName)}/domains/{Uri.EscapeDataString(hostname)}");

        using var response = await http.SendAsync(request, cancellationToken);
        using var payload = await ReadResponseAsync(response, cancellationToken);
        return ParseDomain(payload, hostname);
    }

    public async Task<CloudflarePagesDomainState?> GetDomainAsync(string hostname, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return null;

        EnsureConfigured();

        using var response = await http.GetAsync(
            $"/accounts/{Uri.EscapeDataString(settings.AccountId)}/pages/projects/{Uri.EscapeDataString(settings.ProjectName)}/domains/{Uri.EscapeDataString(hostname)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        using var payload = await ReadResponseAsync(response, cancellationToken);
        return ParseDomain(payload, hostname);
    }

    private void EnsureConfigured()
    {
        if (!settings.Enabled)
            throw new InvalidOperationException("Cloudflare Pages integration is not enabled.");

        if (string.IsNullOrWhiteSpace(settings.AccountId) ||
            string.IsNullOrWhiteSpace(settings.ProjectName) ||
            string.IsNullOrWhiteSpace(settings.ApiToken))
            throw new InvalidOperationException("Cloudflare Pages integration is enabled but AccountId, ProjectName or ApiToken is missing.");
    }

    private async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Cloudflare returned an invalid response with HTTP {(int)response.StatusCode}.");
        }

        var success = doc.RootElement.TryGetProperty("success", out var successValue) &&
                      successValue.ValueKind == JsonValueKind.True;

        if (!response.IsSuccessStatusCode || !success)
        {
            string? message = null;
            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.TryGetProperty("message", out var messageValue))
                    {
                        message = messageValue.GetString();
                        if (!string.IsNullOrWhiteSpace(message))
                            break;
                    }
                }
            }

            doc.Dispose();
            throw new InvalidOperationException(
                message ?? $"Cloudflare request failed with HTTP {(int)response.StatusCode}.");
        }

        return doc;
    }

    private static CloudflarePagesDomainState ParseDomain(JsonDocument payload, string requestedHostname)
    {
        var result = payload.RootElement.GetProperty("result");
        var validation = result.TryGetProperty("validation_data", out var validationData)
            ? validationData
            : default;
        var verification = result.TryGetProperty("verification_data", out var verificationData)
            ? verificationData
            : default;

        string Get(JsonElement parent, string property) =>
            parent.ValueKind != JsonValueKind.Undefined &&
            parent.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";

        return new CloudflarePagesDomainState(
            Get(result, "name") is { Length: > 0 } name ? name : requestedHostname,
            Get(result, "status"),
            Get(validation, "method"),
            Get(validation, "status"),
            string.IsNullOrWhiteSpace(Get(validation, "error_message")) ? null : Get(validation, "error_message"),
            string.IsNullOrWhiteSpace(Get(validation, "txt_name")) ? null : Get(validation, "txt_name"),
            string.IsNullOrWhiteSpace(Get(validation, "txt_value")) ? null : Get(validation, "txt_value"),
            Get(verification, "status"),
            string.IsNullOrWhiteSpace(Get(verification, "error_message")) ? null : Get(verification, "error_message"));
    }
}
