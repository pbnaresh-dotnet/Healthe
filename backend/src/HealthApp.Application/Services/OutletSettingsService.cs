using HealthApp.Application.Abstractions;
using HealthApp.Application.Orchestration;
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
    ICloudflarePagesService cloudflarePages,
    IOutletLegalPolicyRepository legalPolicies,
    IUnitOfWork unitOfWork) : IOutletSettingsService
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
            await BuildReadinessAsync(outlet), MapBranding(branding),
            outlet.DeliveryCoverageMode.ToString(), outlet.ServiceRadiusKm, outlet.Latitude, outlet.Longitude, outlet.Slug);
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
        branding.FontFamily = NormalizeFontFamily(request.FontFamily);
        branding.ThemeStyle = NormalizeThemeStyle(request.ThemeStyle);
        branding.ButtonStyle = NormalizeButtonStyle(request.ButtonStyle);
        branding.CardStyle = NormalizeCardStyle(request.CardStyle);
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
            About = outlet.About ?? string.Empty,
            FontFamily = "Inter",
            ThemeStyle = "Fresh",
            ButtonStyle = "Rounded",
            CardStyle = "Soft"
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
            branding.FooterText,
            branding.FontFamily,
            branding.ThemeStyle,
            branding.ButtonStyle,
            branding.CardStyle);

    private static string NormalizeFontFamily(string? value)
    {
        var v = (value ?? "Inter").Trim();
        return v is "Inter" or "Poppins" or "DM Sans" or "Nunito" or "Manrope" ? v : "Inter";
    }

    private static string NormalizeThemeStyle(string? value)
    {
        var v = (value ?? "Fresh").Trim();
        return v is "Fresh" or "Modern" or "Premium" or "Minimal" ? v : "Fresh";
    }

    private static string NormalizeButtonStyle(string? value)
    {
        var v = (value ?? "Rounded").Trim();
        return v is "Rounded" or "Pill" or "Square" ? v : "Rounded";
    }

    private static string NormalizeCardStyle(string? value)
    {
        var v = (value ?? "Soft").Trim();
        return v is "Soft" or "Elevated" or "Flat" ? v : "Soft";
    }

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

        if (!string.IsNullOrWhiteSpace(request.DeliveryCoverageMode))
        {
            if (!Enum.TryParse<DeliveryCoverageMode>(request.DeliveryCoverageMode, true, out var coverageMode))
                throw new ArgumentException("Delivery coverage mode must be Radius or Areas.");
            outlet.DeliveryCoverageMode = coverageMode;
        }
        if (request.ServiceRadiusKm.HasValue)
        {
            if (request.ServiceRadiusKm.Value < 1 || request.ServiceRadiusKm.Value > 100)
                throw new ArgumentException("Delivery radius must be between 1 and 100 km.");
            outlet.ServiceRadiusKm = request.ServiceRadiusKm.Value;
        }

        await outlets.UpdateAsync(outlet);
        return await GetAsync();
    }

    public async Task<OutletLegalPoliciesDto?> GetLegalPoliciesAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId);
        if (outlet is null) return null;

        var published = await legalPolicies.GetPublishedAsync(outletId);
        var history = await legalPolicies.GetHistoryAsync(outletId);
        return MapLegalPolicies(outlet, published, history);
    }

    public async Task<OutletLegalPoliciesDto?> UpdateLegalPoliciesAsync(UpdateOutletLegalPoliciesRequest request)
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        var version = string.IsNullOrWhiteSpace(request.LegalVersion) ? "1.0" : request.LegalVersion.Trim();
        if (version.Length > 40)
            throw new ArgumentException("Legal document version must be 40 characters or fewer.");

        outlet.CustomerTermsAndConditions = NormalizePolicy(request.CustomerTermsAndConditions, "Customer Terms & Conditions");
        outlet.CustomerPrivacyPolicy = NormalizePolicy(request.CustomerPrivacyPolicy, "Customer Privacy Policy");
        outlet.CancellationRefundPolicy = NormalizePolicy(request.CancellationRefundPolicy, "Cancellation & Refund Policy");
        outlet.MealSkipReschedulePolicy = NormalizePolicy(request.MealSkipReschedulePolicy, "Meal Skip & Rescheduling Policy");
        outlet.DeliveryPolicy = NormalizePolicy(request.DeliveryPolicy, "Delivery Policy");
        outlet.AllergenDietaryDisclaimer = NormalizePolicy(request.AllergenDietaryDisclaimer, "Allergen & Dietary Disclaimer");
        outlet.PaymentPricingPromotionalTerms = NormalizePolicy(request.PaymentPricingPromotionalTerms, "Payment, Pricing & Promotional Terms");
        outlet.LegalVersion = version;

        var published = await legalPolicies.GetPublishedAsync(outletId);
        if (request.LegalPoliciesPublished)
        {
            if (string.IsNullOrWhiteSpace(outlet.CustomerTermsAndConditions) ||
                string.IsNullOrWhiteSpace(outlet.CustomerPrivacyPolicy) ||
                string.IsNullOrWhiteSpace(outlet.CancellationRefundPolicy) ||
                string.IsNullOrWhiteSpace(outlet.MealSkipReschedulePolicy) ||
                string.IsNullOrWhiteSpace(outlet.DeliveryPolicy) ||
                string.IsNullOrWhiteSpace(outlet.AllergenDietaryDisclaimer) ||
                string.IsNullOrWhiteSpace(outlet.PaymentPricingPromotionalTerms))
                throw new ArgumentException("All customer-facing legal and commercial policies must be completed before publishing.");

            if (!request.LegalEffectiveDateUtc.HasValue)
                throw new ArgumentException("An effective date is required before publishing.");
            if (request.LegalEffectiveDateUtc.Value > DateTime.UtcNow.AddMinutes(1))
                throw new ArgumentException("The effective date cannot be in the future because publishing is immediate.");

            if (published is not null && published.Version == version)
            {
                var requestedHash = ComputePolicyHash(outlet);
                if (!string.Equals(published.ContentHash, requestedHash, StringComparison.OrdinalIgnoreCase) ||
                    published.EffectiveDateUtc.Date != request.LegalEffectiveDateUtc.Value.Date)
                    throw new InvalidOperationException($"Legal version {version} is already published and immutable. Increase the version number before publishing revised content.");
            }
            else
            {
                var existingVersion = await legalPolicies.GetByVersionAsync(outletId, version);
                if (existingVersion is not null)
                    throw new InvalidOperationException($"Legal version {version} already exists. Use a new version number.");

                // Version creation is performed atomically with the outlet update below.
            }

            outlet.LegalEffectiveDateUtc = request.LegalEffectiveDateUtc.Value;
            outlet.LegalPoliciesPublished = true;
        }
        else
        {
            // Saving a draft never changes or deletes the currently published customer version.
            // It only updates the editable outlet draft fields.
            outlet.LegalEffectiveDateUtc = request.LegalEffectiveDateUtc;
            outlet.LegalPoliciesPublished = published is not null;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            if (request.LegalPoliciesPublished && (published is null || published.Version != version))
            {
                var contentHash = ComputePolicyHash(outlet);
                await legalPolicies.PublishVersionAsync(new OutletLegalPolicyVersion
                {
                    Id = Guid.NewGuid(),
                    OutletId = outletId,
                    Version = version,
                    CustomerTermsAndConditions = outlet.CustomerTermsAndConditions,
                    CustomerPrivacyPolicy = outlet.CustomerPrivacyPolicy,
                    CancellationRefundPolicy = outlet.CancellationRefundPolicy,
                    MealSkipReschedulePolicy = outlet.MealSkipReschedulePolicy,
                    DeliveryPolicy = outlet.DeliveryPolicy,
                    AllergenDietaryDisclaimer = outlet.AllergenDietaryDisclaimer,
                    PaymentPricingPromotionalTerms = outlet.PaymentPricingPromotionalTerms,
                    ContentHash = contentHash,
                    EffectiveDateUtc = request.LegalEffectiveDateUtc!.Value,
                    CreatedAtUtc = DateTime.UtcNow,
                    PublishedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = current.UserId,
                    IsPublished = true
                });
            }
            await outlets.UpdateAsync(outlet);
        });
        var latestPublished = await legalPolicies.GetPublishedAsync(outletId);
        var history = await legalPolicies.GetHistoryAsync(outletId);
        return MapLegalPolicies(outlet, latestPublished, history);
    }

    private static string ComputePolicyHash(Outlet outlet)
    {
        var canonical = string.Join("\n---\n", new[]
        {
            outlet.CustomerTermsAndConditions ?? string.Empty,
            outlet.CustomerPrivacyPolicy ?? string.Empty,
            outlet.CancellationRefundPolicy ?? string.Empty,
            outlet.MealSkipReschedulePolicy ?? string.Empty,
            outlet.DeliveryPolicy ?? string.Empty,
            outlet.AllergenDietaryDisclaimer ?? string.Empty,
            outlet.PaymentPricingPromotionalTerms ?? string.Empty
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string NormalizePolicy(string? value, string name)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > 100000)
            throw new ArgumentException($"{name} is too long. Keep each policy under 100,000 characters.");
        return text;
    }

    private static OutletLegalPoliciesDto MapLegalPolicies(
        Outlet outlet,
        OutletLegalPolicyVersion? published,
        IReadOnlyList<OutletLegalPolicyVersion> history)
        => new(
            outlet.Id,
            outlet.Name,
            outlet.CustomerTermsAndConditions ?? string.Empty,
            outlet.CustomerPrivacyPolicy ?? string.Empty,
            outlet.CancellationRefundPolicy ?? string.Empty,
            outlet.MealSkipReschedulePolicy ?? string.Empty,
            outlet.DeliveryPolicy ?? string.Empty,
            outlet.AllergenDietaryDisclaimer ?? string.Empty,
            outlet.PaymentPricingPromotionalTerms ?? string.Empty,
            outlet.LegalVersion ?? "1.0",
            outlet.LegalEffectiveDateUtc,
            published is not null,
            published?.Id,
            published?.Version ?? string.Empty,
            published?.EffectiveDateUtc,
            history.Select(x => new OutletLegalPolicyVersionDto(
                x.Id, x.Version, x.EffectiveDateUtc, x.CreatedAtUtc, x.PublishedAtUtc, x.IsPublished, x.ContentHash)).ToList());

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
        var coverageReady = outlet.DeliveryCoverageMode == DeliveryCoverageMode.Radius
            ? outlet.ServiceRadiusKm >= 1
            : selectedAreas > 0;
        var deliveryDays = ParseDays(outlet.DeliveryDays);
        var publishedLegal = await legalPolicies.GetPublishedAsync(outletId);
        var legalReady = publishedLegal is not null &&
            !string.IsNullOrWhiteSpace(publishedLegal.CustomerTermsAndConditions) &&
            !string.IsNullOrWhiteSpace(publishedLegal.CustomerPrivacyPolicy) &&
            !string.IsNullOrWhiteSpace(publishedLegal.CancellationRefundPolicy) &&
            !string.IsNullOrWhiteSpace(publishedLegal.MealSkipReschedulePolicy) &&
            !string.IsNullOrWhiteSpace(publishedLegal.DeliveryPolicy) &&
            !string.IsNullOrWhiteSpace(publishedLegal.AllergenDietaryDisclaimer) &&
            !string.IsNullOrWhiteSpace(publishedLegal.PaymentPricingPromotionalTerms);

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
            new("delivery-areas", "Delivery coverage", "Choose an outlet service radius on the map or select approved city areas.",
                coverageReady, outlet.DeliveryCoverageMode == DeliveryCoverageMode.Radius ? 1 : selectedAreas, 1, "delivery-areas"),
            new("delivery-pricing", "Delivery pricing", "Configure at least one distance-based delivery fee.",
                pricingRules > 0, pricingRules, 1, "pricing"),
            new("tax", "Tax settings", "Confirm the restaurant GST rate for customer pricing.",
                outlet.RestaurantGstRate >= 0m, 1, 1, "tax"),
            new("legal", "Customer legal policies", "Complete and publish an immutable Terms, Privacy and customer-policy version before accepting registrations and orders.",
                legalReady, legalReady ? 1 : 0, 1, "legal")
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
