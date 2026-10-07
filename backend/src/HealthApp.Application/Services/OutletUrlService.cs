using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using Microsoft.Extensions.Options;

namespace HealthApp.Application.Services;

public sealed class OutletUrlService(
    IOutletRepository outlets,
    IOutletDomainRepository domains,
    IOptions<TenantDomainSettings> domainSettings) : IOutletUrlService
{
    public async Task<string> GetStorefrontUrlAsync(Guid outletId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var custom = (await domains.GetByOutletAsync(outletId))
            .Where(x => x.Status == OutletDomainStatus.Active &&
                        x.IsPrimary &&
                        !string.IsNullOrWhiteSpace(x.Hostname))
            .OrderByDescending(x => x.VerifiedAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefault();

        var hostname = custom?.Hostname?.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(hostname))
        {
            var outlet = await outlets.GetByIdAsync(outletId)
                ?? throw new KeyNotFoundException("Outlet not found.");

            var subdomain = outlet.Subdomain.Trim().Trim('.');
            var baseDomain = domainSettings.Value.PlatformBaseDomain.Trim().Trim('.');
            if (string.IsNullOrWhiteSpace(subdomain) || string.IsNullOrWhiteSpace(baseDomain))
                throw new InvalidOperationException("Customer storefront domain is not configured for this outlet.");

            hostname = $"{subdomain}.{baseDomain}";
        }

        if (!Uri.TryCreate($"https://{hostname}", UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("Customer storefront domain is invalid.");

        return uri.AbsoluteUri.TrimEnd('/');
    }
}
