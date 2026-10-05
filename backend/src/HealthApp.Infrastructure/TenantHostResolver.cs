using System.Globalization;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure;

public sealed class TenantHostResolver(
    IOutletRepository outlets,
    IOutletDomainRepository domains,
    IOptions<TenantDomainSettings> settings) : ITenantHostResolver
{
    public async Task<Outlet?> ResolveAsync(string? hostname)
    {
        var host = NormalizeHostname(hostname);
        if (host.Length == 0) return null;

        // Explicit custom-domain mappings take precedence over platform subdomains.
        var custom = await domains.GetActiveByHostnameAsync(host);
        if (custom?.Outlet is not null)
            return custom.Outlet;

        var baseDomain = NormalizeHostname(settings.Value.PlatformBaseDomain);
        if (baseDomain.Length == 0 || string.Equals(host, baseDomain, StringComparison.OrdinalIgnoreCase))
            return null;

        var suffix = "." + baseDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var subdomain = host[..^suffix.Length];
        // Only a single tenant label is accepted. This avoids treating unrelated
        // hosts such as a.b.healthapp.com as tenant "a".
        if (subdomain.Length == 0 || subdomain.Contains('.', StringComparison.Ordinal))
            return null;

        // Common platform infrastructure hosts are never treated as tenants.
        if (subdomain is "www" or "api" or "admin" or "outlet")
            return null;

        return await outlets.GetBySubdomainAsync(subdomain);
    }

    private static string NormalizeHostname(string? value)
    {
        var host = (value ?? string.Empty).Trim().TrimEnd('.');
        if (host.Length == 0 || host.Contains("://", StringComparison.Ordinal) || host.Contains('/', StringComparison.Ordinal))
            return string.Empty;

        try
        {
            var idn = new IdnMapping();
            return idn.GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
