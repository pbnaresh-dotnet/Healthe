using HealthApp.Application.Abstractions;
using HealthApp.Application.Strategies;
using HealthApp.Domain.Enums;

namespace HealthApp.Application.Services;

public sealed class FinanceTaxConfigurationService(
    IOutletRepository outlets,
    IOutletTaxProfileRepository profiles,
    IFinanceTaxRuleRepository taxRules) : IFinanceTaxConfigurationService
{
    public async Task<FinanceTaxCalculationConfiguration> ResolveAsync(Guid outletId, DateTime asOfUtc)
    {
        var outlet = await outlets.GetByIdAsync(outletId)
            ?? throw new KeyNotFoundException("Outlet not found.");

        var profile = await profiles.GetCurrentAsync(outletId, asOfUtc);
        if (profile is null)
        {
            // Existing legacy outlets created before finance governance can still be read
            // safely. They are treated as direct, non-GST-registered until an admin
            // explicitly creates/publishes their tax profile.
            profile = new Domain.Entities.OutletTaxProfile
            {
                Id = Guid.Empty,
                OutletId = outletId,
                LegalName = outlet.Name,
                TradeName = outlet.Name,
                City = outlet.City,
                State = outlet.State,
                PostalCode = outlet.Pincode,
                Country = "India",
                IsGstRegistered = false,
                IsComposition = false,
                RestaurantGstRate = outlet.RestaurantGstRate,
                RestaurantGstMode = outlet.RestaurantGstMode,
                TaxOperatingMode = TaxOperatingMode.DirectOutletSupplier,
                EffectiveFromUtc = asOfUtc,
                IsActive = true
            };
        }

        var restaurantRule = await taxRules.GetEffectiveAsync(
            FinanceSupplyType.RestaurantSale,
            profile.TaxOperatingMode,
            asOfUtc)
            ?? throw new InvalidOperationException(
                $"No effective restaurant tax rule is configured for {asOfUtc:O}.");

        var platformRule = await taxRules.GetEffectiveAsync(
            FinanceSupplyType.PlatformSaaS,
            null,
            asOfUtc)
            ?? throw new InvalidOperationException(
                $"No effective HealthApp platform-service tax rule is configured for {asOfUtc:O}.");

        var restaurantApplicable =
            profile.TaxOperatingMode == TaxOperatingMode.EcoSection9_5 ||
            (profile.TaxOperatingMode == TaxOperatingMode.DirectOutletSupplier &&
             profile.IsGstRegistered &&
             !profile.IsComposition);

        var restaurant = new RestaurantTaxConfiguration(
            profile.Id,
            restaurantApplicable,
            profile.IsGstRegistered,
            profile.IsComposition,
            profile.TaxOperatingMode,
            profile.RestaurantGstMode,
            ToSnapshot(restaurantRule));

        return new FinanceTaxCalculationConfiguration(
            restaurant,
            ToSnapshot(platformRule));
    }

    private static FinanceTaxRuleSnapshot ToSnapshot(HealthApp.Domain.Entities.FinanceTaxRule rule)
        => new(
            rule.Id,
            rule.Code,
            rule.SupplyType,
            rule.TaxOperatingMode,
            rule.TaxRatePercent,
            rule.CgstRatePercent,
            rule.SgstRatePercent,
            rule.IgstRatePercent,
            rule.EffectiveFromUtc,
            rule.EffectiveToUtc);
}
