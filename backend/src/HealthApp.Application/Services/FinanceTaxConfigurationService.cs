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
            // Existing/new outlets created outside the finance onboarding path receive a
            // real initial profile before any transaction can depend on it.
            var initial = new Domain.Entities.OutletTaxProfile
            {
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
                TaxOperatingMode = TaxOperatingMode.DirectOutletSupplier
            };
            await profiles.AddVersionAsync(initial, asOfUtc);
            profile = await profiles.GetCurrentAsync(outletId, asOfUtc)
                ?? throw new InvalidOperationException("Unable to create the outlet's initial finance tax profile.");
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

        var commissionRule = await taxRules.GetEffectiveAsync(
            FinanceSupplyType.PlatformCommission,
            null,
            asOfUtc)
            ?? throw new InvalidOperationException(
                $"No effective HealthApp commission tax rule is configured for {asOfUtc:O}.");

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
            ToSnapshot(platformRule),
            ToSnapshot(commissionRule));
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
