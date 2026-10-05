using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;
using Microsoft.Extensions.Options;

namespace HealthApp.Application.Services;

public sealed class OutletSettingsService(
    ICurrentUser current,
    IOutletRepository outlets,
    IUserRepository users,
    IRecipeRepository recipes,
    IMealPlanRepository mealPlans,
    IOutletMenuRepository menu,
    IOutletDeliveryAreaRepository deliveryAreas,
    IDeliveryPricingRepository pricing,
    IOutletSubscriptionRepository outletSubscriptions,
    IOutletBrandingRepository brandingRepository,
    IOutletDomainRepository domains,
    IOptions<TenantDomainSettings> domainSettings,
    ICloudflarePagesService cloudflarePages) : IOutletSettingsService
{
    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public async Task<OutletSettingsDto?> GetAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId);
        if (outlet is null) return null;
        var branding = await EnsureBrandingAsync(outlet);
        return new(
            outlet.Id, outlet.Name, outlet.City, outlet.State, outlet.Pincode,
            outlet.DeliveryDays, outlet.RestaurantGstRate, outlet.RestaurantGstMode.ToString(),
            await BuildReadinessAsync(outlet), MapBranding(branding));
    }

    public async Task<IReadOnlyList<OutletDomainDto>> GetDomainsAsync()
    {
        if (current.OutletId is not Guid outletId) return [];

        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var baseDomain = NormalizeHostname(domainSettings.Value.PlatformBaseDomain);
        var result = new List<OutletDomainDto>();

        if (!string.IsNullOrWhiteSpace(outlet.Subdomain) && !string.IsNullOrWhiteSpace(baseDomain))
        {
            result.Add(new OutletDomainDto(
                Guid.Empty,
                outlet.Id,
                outlet.Name,
                $"{outlet.Subdomain.Trim().ToLowerInvariant()}.{baseDomain}",
                "Platform",
                OutletDomainStatus.Active.ToString(),
                true,
                DateTime.MinValue,
                null,
                "",
                "",
                "",
                "Cloudflare Pages",
                "active",
                "active",
                null));
        }

        var custom = await domains.GetByOutletAsync(outletId);
        result.AddRange(custom.Select(x => MapDomain(x, baseDomain)));
        return result;
    }

    public async Task<OutletDomainDto> RequestDomainAsync(RequestOutletDomainRequest request)
    {
        if (current.OutletId is not Guid outletId) throw new UnauthorizedAccessException("The current user is not associated with an outlet.");
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        if (!cloudflarePages.IsEnabled)
            throw new InvalidOperationException("Custom domains require the Cloudflare Pages integration to be enabled.");

        var hostname = NormalizeHostname(request.Hostname);
        var baseDomain = NormalizeHostname(domainSettings.Value.PlatformBaseDomain);
        ValidateCustomHostname(hostname, baseDomain);

        var existing = await domains.GetByHostnameAsync(hostname);
        if (existing is not null && existing.OutletId != outletId)
            throw new InvalidOperationException("This domain is already assigned to another outlet.");

        var domain = existing ?? new OutletDomain
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            Hostname = hostname,
            VerificationToken = Guid.NewGuid().ToString("N"),
            Status = OutletDomainStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (domain.Status == OutletDomainStatus.Active)
            return MapDomain(domain, baseDomain);

        domain.Status = OutletDomainStatus.Pending;
        domain.VerificationToken = Guid.NewGuid().ToString("N");
        domain.VerificationRecordName = $"_healthapp-verification.{hostname}";
        domain.IsPrimary = request.IsPrimary;
        domain.VerifiedAtUtc = null;

        var providerState = cloudflarePages.IsEnabled
            ? await cloudflarePages.EnsureDomainAsync(hostname)
            : null;

        if (providerState is not null)
            ApplyProviderState(domain, providerState);

        if (existing is null)
            await domains.AddAsync(domain);
        else
            await domains.UpdateAsync(domain);

        return MapDomain(domain, baseDomain, providerState);
    }

    public async Task<OutletDomainDto> VerifyDomainAsync(Guid domainId, bool activateIfReady = false)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var domain = await domains.GetAsync(domainId) ?? throw new KeyNotFoundException("Outlet domain not found.");
        if (domain.OutletId != outletId)
            throw new UnauthorizedAccessException("The outlet domain does not belong to the current outlet.");

        var outlet = domain.Outlet ?? await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var baseDomain = NormalizeHostname(domainSettings.Value.PlatformBaseDomain);

        CloudflarePagesDomainState? providerState = null;
        if (cloudflarePages.IsEnabled)
        {
            providerState = await cloudflarePages.GetDomainAsync(domain.Hostname);
            if (providerState is null)
                throw new InvalidOperationException("Cloudflare Pages has not attached this domain yet.");

            if (!IsCloudflareActive(providerState))
                providerState = await cloudflarePages.RetryValidationAsync(domain.Hostname) ?? providerState;

            ApplyProviderState(domain, providerState);

            if (IsCloudflareActive(providerState))
            {
                domain.VerifiedAtUtc ??= DateTime.UtcNow;
                domain.Status = activateIfReady && outlet.Status == OutletStatus.Live &&
                    await outletSubscriptions.GetByOutletAsync(outletId) is { Status: "Active" }
                    ? OutletDomainStatus.Active
                    : OutletDomainStatus.Verified;
            }
        }
        else
        {
            throw new InvalidOperationException("Cloudflare Pages integration is not enabled. Configure it before checking DNS verification.");
        }

        await domains.UpdateAsync(domain);
        return MapDomain(domain, baseDomain, providerState);
    }

    private static void ApplyProviderState(OutletDomain domain, CloudflarePagesDomainState state)
    {
        domain.VerificationRecordName = state.TxtName ?? domain.VerificationRecordName;
        domain.VerificationToken = state.TxtValue ?? domain.VerificationToken;

        if (IsCloudflareActive(state))
            domain.VerifiedAtUtc ??= DateTime.UtcNow;
    }

    private static bool IsCloudflareActive(CloudflarePagesDomainState state) =>
        string.Equals(state.Status, "active", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(state.ValidationStatus, "active", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(state.VerificationStatus, "active", StringComparison.OrdinalIgnoreCase);

    private OutletDomainDto MapDomain(OutletDomain x, string baseDomain, CloudflarePagesDomainState? providerState = null)
    {
        var isCustom = string.IsNullOrWhiteSpace(baseDomain) || !x.Hostname.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase);
        return new(
            x.Id,
            x.OutletId,
            x.Outlet?.Name ?? "",
            x.Hostname,
            isCustom ? "Custom" : "Platform",
            x.Status.ToString(),
            x.IsPrimary,
            x.CreatedAtUtc,
            x.VerifiedAtUtc,
            providerState?.ValidationMethod ?? "TXT",
            x.VerificationRecordName,
            x.VerificationToken,
            cloudflarePages.IsEnabled ? "Cloudflare Pages" : "Manual",
            providerState?.Status ?? "not_checked",
            providerState?.ValidationStatus ?? "not_checked",
            providerState?.ValidationError ?? providerState?.VerificationError);
    }

    private static string NormalizeHostname(string? value)
    {
        var host = (value ?? "").Trim().TrimEnd('.');
        if (host.Length == 0 || host.Contains("://", StringComparison.Ordinal) || host.Contains('/', StringComparison.Ordinal) || host.Contains(' ', StringComparison.Ordinal))
            throw new ArgumentException("Enter a valid domain name, for example www.fitfood.com.");

        try
        {
            var idn = new System.Globalization.IdnMapping();
            return idn.GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Enter a valid domain name.");
        }
    }

    private static void ValidateCustomHostname(string hostname, string baseDomain)
    {
        if (hostname.Length > 253 || hostname.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException("The domain name is too long or invalid.");

        if (System.Net.IPAddress.TryParse(hostname, out _))
            throw new ArgumentException("Use a domain name rather than an IP address.");

        if (!hostname.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException("Enter a full custom domain such as www.fitfood.com.");

        if (!string.IsNullOrWhiteSpace(baseDomain) &&
            (string.Equals(hostname, baseDomain, StringComparison.OrdinalIgnoreCase) ||
             hostname.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Use the outlet platform subdomain for healthapp.com. Custom domain requests must use your own domain.");
    }

    public async Task<OutletBrandingDto?> UpdateBrandingAsync(UpdateOutletBrandingRequest request)
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        if (string.IsNullOrWhiteSpace(request.BrandName) || request.BrandName.Trim().Length > 200)
            throw new ArgumentException("Brand name is required and must be 200 characters or fewer.");
        if (request.Tagline?.Trim().Length > 300)
            throw new ArgumentException("Tagline must be 300 characters or fewer.");
        if (request.HealthHighlights?.Trim().Length > 2000)
            throw new ArgumentException("Health highlights must be 2,000 characters or fewer.");
        if (request.About?.Trim().Length > 4000)
            throw new ArgumentException("About text must be 4,000 characters or fewer.");
        if (request.FooterText?.Trim().Length > 1000)
            throw new ArgumentException("Footer text must be 1,000 characters or fewer.");

        var primary = NormalizeColor(request.PrimaryColor, "#14532d");
        var secondary = NormalizeColor(request.SecondaryColor, "#166534");
        var branding = await EnsureBrandingAsync(outlet);

        branding.BrandName = request.BrandName.Trim();
        branding.Tagline = (request.Tagline ?? string.Empty).Trim();
        branding.PrimaryColor = primary;
        branding.SecondaryColor = secondary;
        branding.HealthHighlights = (request.HealthHighlights ?? string.Empty).Trim();
        branding.About = (request.About ?? string.Empty).Trim();
        branding.FooterText = (request.FooterText ?? string.Empty).Trim();
        branding.UpdatedAtUtc = DateTime.UtcNow;

        // Keep legacy Outlet columns synchronized for older labels/reports while the
        // dedicated OutletBranding row remains the authoritative presentation model.
        outlet.LogoUrl = branding.LogoUrl;
        outlet.HeroImageUrl = branding.HeroImageUrl;
        outlet.PrimaryColor = branding.PrimaryColor;
        outlet.HealthHighlights = branding.HealthHighlights;
        outlet.About = branding.About;

        await brandingRepository.UpdateAsync(branding);
        return MapBranding(branding);
    }

    public async Task<OutletBrandingDto?> UpdateBrandingAssetAsync(string assetType, string url)
    {
        if (current.OutletId is not Guid outletId) return null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 1000)
            throw new ArgumentException("The uploaded asset URL is invalid.");

        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var branding = await EnsureBrandingAsync(outlet);

        switch ((assetType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "logo":
                branding.LogoUrl = url;
                outlet.LogoUrl = url;
                break;
            case "hero":
                branding.HeroImageUrl = url;
                outlet.HeroImageUrl = url;
                break;
            case "favicon":
                branding.FaviconUrl = url;
                break;
            default:
                throw new ArgumentException("Supported branding assets are: logo, hero and favicon.");
        }

        branding.UpdatedAtUtc = DateTime.UtcNow;
        await brandingRepository.UpdateAsync(branding);
        return MapBranding(branding);
    }

    private async Task<OutletBranding> EnsureBrandingAsync(HealthApp.Domain.Entities.Outlet outlet)
    {
        var branding = await brandingRepository.GetByOutletAsync(outlet.Id);
        if (branding is not null) return branding;

        branding = new OutletBranding
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            BrandName = string.IsNullOrWhiteSpace(outlet.Name) ? "Outlet" : outlet.Name,
            LogoUrl = outlet.LogoUrl ?? string.Empty,
            HeroImageUrl = outlet.HeroImageUrl ?? string.Empty,
            PrimaryColor = string.IsNullOrWhiteSpace(outlet.PrimaryColor) ? "#14532d" : outlet.PrimaryColor,
            SecondaryColor = "#166534",
            HealthHighlights = outlet.HealthHighlights ?? string.Empty,
            About = outlet.About ?? string.Empty
        };
        await brandingRepository.AddAsync(branding);
        return branding;
    }

    private static OutletBrandingDto MapBranding(OutletBranding branding)
        => new(
            branding.BrandName,
            branding.Tagline,
            branding.LogoUrl,
            branding.HeroImageUrl,
            branding.FaviconUrl,
            branding.PrimaryColor,
            branding.SecondaryColor,
            (branding.HealthHighlights ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            branding.About,
            branding.FooterText);

    private static string NormalizeColor(string? value, string fallback)
    {
        var color = (value ?? string.Empty).Trim();
        if (color.Length == 0) return fallback;
        if (System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))
            return color;
        throw new ArgumentException("Brand colours must be six-digit hexadecimal values such as #14532d.");
    }

    public async Task<OutletSettingsDto?> UpdateDeliveryDaysAsync(UpdateOutletSettingsRequest request)
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        var days = ParseDays(request.DeliveryDays);
        if (days.Count == 0)
            throw new ArgumentException("Select at least one delivery day.");

        outlet.DeliveryDays = string.Join(",", Weekdays
            .Where(days.Contains)
            .Select(x => x.ToString()));

        await outlets.UpdateAsync(outlet);
        return await GetAsync();
    }

    public async Task<OutletReadinessDto?> GetReadinessAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId);
        return outlet is null ? null : await BuildReadinessAsync(outlet);
    }

    public async Task<OutletReadinessDto?> GoLiveAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        if (outlet.Status == OutletStatus.Suspended)
            throw new InvalidOperationException("A suspended outlet cannot go live.");

        if (outlet.Status != OutletStatus.Active && outlet.Status != OutletStatus.Live)
            throw new InvalidOperationException("The outlet must be activated by Super Admin before it can go live.");

        var outletSubscription = await outletSubscriptions.GetByOutletAsync(outletId);
        if (outletSubscription is null || !string.Equals(outletSubscription.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The outlet SaaS subscription must be active before the customer portal can go live.");

        var user = current.UserId is Guid userId ? await users.FindByIdAsync(userId) : null;
        if (user?.IsDemo == true)
            throw new InvalidOperationException("Demo accounts cannot be published to the customer marketplace.");

        var readiness = await BuildReadinessAsync(outlet);
        if (!readiness.CanGoLive)
            throw new InvalidOperationException("Complete every required setup item before going live.");

        if (outlet.Status != OutletStatus.Live)
        {
            outlet.Status = OutletStatus.Live;
            await outlets.UpdateAsync(outlet);
        }

        return await BuildReadinessAsync(outlet);
    }

    private async Task<OutletReadinessDto> BuildReadinessAsync(HealthApp.Domain.Entities.Outlet outlet)
    {
        var outletId = outlet.Id;
        var activeRecipes = (await recipes.GetByOutletAsync(outletId)).Count(x => x.IsActive);
        var activePlans = (await mealPlans.GetByOutletAsync(outletId)).Count(x => x.IsActive);
        var menuItems = (await menu.GetByOutletAsync(outletId)).Where(x => x.IsAvailable).ToList();
        var activeDrivers = (await users.GetAllAsync()).Count(x =>
            x.OutletId == outletId &&
            x.Role == UserRole.Driver &&
            x.IsActive &&
            (!x.IsDemo || !x.DemoExpiresAtUtc.HasValue || x.DemoExpiresAtUtc.Value > DateTime.UtcNow));
        var selectedAreas = (await deliveryAreas.GetByOutletAsync(outletId)).Count;
        var pricingRules = (await pricing.GetByOutletAsync(outletId)).Count;
        var deliveryDays = ParseDays(outlet.DeliveryDays);

        var menuDaysReady = deliveryDays.Count > 0
            ? deliveryDays.Count(day => menuItems.Any(x => x.DayOfWeek == day))
            : 0;

        var items = new List<OutletReadinessItemDto>
        {
            new("business", "Business profile", "Outlet name, city, state and pincode must be configured.", 
                !string.IsNullOrWhiteSpace(outlet.Name) && !string.IsNullOrWhiteSpace(outlet.City) &&
                !string.IsNullOrWhiteSpace(outlet.State) && !string.IsNullOrWhiteSpace(outlet.Pincode), 1, 1, "business"),
            new("delivery-days", "Delivery days", "Choose the days your kitchen accepts subscription deliveries.",
                deliveryDays.Count > 0, deliveryDays.Count, 1, "delivery-days"),
            new("recipes", "Recipes", "Add at least one active recipe to your outlet menu.",
                activeRecipes > 0, activeRecipes, 1, "recipes"),
            new("meal-plans", "Meal plans", "Create at least one active customer meal plan.",
                activePlans > 0, activePlans, 1, "meal-plans"),
            new("menu", "Weekly menu", "Every selected delivery day must have at least one available menu item.",
                deliveryDays.Count > 0 && menuDaysReady == deliveryDays.Count, menuDaysReady, Math.Max(1, deliveryDays.Count), "menu"),
            new("drivers", "Drivers", "Add at least one active delivery driver.",
                activeDrivers > 0, activeDrivers, 1, "drivers"),
            new("delivery-areas", "Delivery areas", "Select at least one approved city delivery area.",
                selectedAreas > 0, selectedAreas, 1, "delivery-areas"),
            new("delivery-pricing", "Delivery pricing", "Configure at least one distance-based delivery fee.",
                pricingRules > 0, pricingRules, 1, "pricing"),
            new("tax", "Tax settings", "Confirm the restaurant GST rate for customer pricing.",
                outlet.RestaurantGstRate >= 0m, 1, 1, "tax")
        };

        var currentUser = current.UserId is Guid userId ? await users.FindByIdAsync(userId) : null;
        var isDemo = currentUser?.IsDemo == true;
        var canGoLive = !isDemo && (outlet.Status == OutletStatus.Live || items.All(x => x.IsComplete));
        var missing = items.Where(x => !x.IsComplete).Select(x => x.Title).ToList();
        var message = outlet.Status == OutletStatus.Live
            ? "Outlet is live and available to customers."
            : outlet.Status == OutletStatus.Active
                ? isDemo ? "Demo accounts can explore the full workspace but are not published to customers." : canGoLive ? "All required setup is complete. Your outlet is ready to go live." : $"Complete: {string.Join(", ", missing)}."
                : "Waiting for Super Admin activation.";

        return new(outlet.Status.ToString(), outlet.Status == OutletStatus.Live, canGoLive, items, message);
    }

    private static HashSet<DayOfWeek> ParseDays(string? value)
    {
        var result = new HashSet<DayOfWeek>();
        foreach (var token in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DayOfWeek>(token, true, out var day))
                result.Add(day);
        return result;
    }
}
