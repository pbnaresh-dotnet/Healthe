using HealthApp.Domain.Enums;

namespace HealthApp.Application.Strategies;

public sealed record TaxBreakdown(
    decimal RestaurantRate,
    GstMode RestaurantMode,
    decimal RestaurantTaxableAmount,
    decimal RestaurantAmount,
    decimal PlatformRate,
    decimal PlatformAmount,
    bool RestaurantTaxApplicable = true,
    Guid? RestaurantTaxRuleId = null,
    string RestaurantTaxRuleCode = "",
    Guid? PlatformTaxRuleId = null,
    string PlatformTaxRuleCode = "",
    decimal RestaurantCgstRate = 0m,
    decimal RestaurantSgstRate = 0m,
    decimal RestaurantIgstRate = 0m,
    decimal PlatformCgstRate = 0m,
    decimal PlatformSgstRate = 0m,
    decimal PlatformIgstRate = 0m);

public interface ITaxStrategy
{
    TaxBreakdown Calculate(
        decimal restaurantAmount,
        decimal platformServiceFee,
        RestaurantTaxConfiguration restaurant,
        FinanceTaxRuleSnapshot platformServiceTaxRule);
}

public sealed class ConfigurableTaxStrategy : ITaxStrategy
{
    public TaxBreakdown Calculate(
        decimal restaurantAmount,
        decimal platformServiceFee,
        RestaurantTaxConfiguration restaurant,
        FinanceTaxRuleSnapshot platformServiceTaxRule)
    {
        if (restaurant.Rule.TaxRatePercent < 0m || restaurant.Rule.TaxRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(restaurant), "Restaurant tax rate must be between 0% and 100%.");

        if (platformServiceTaxRule.TaxRatePercent < 0m || platformServiceTaxRule.TaxRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(platformServiceTaxRule), "Platform tax rate must be between 0% and 100%.");

        if (restaurant.Rule.TaxRatePercent > 0m)
        {
            var split = restaurant.Rule.CgstRatePercent + restaurant.Rule.SgstRatePercent;
            if (split > 0m && split > restaurant.Rule.TaxRatePercent + 0.0001m)
                throw new InvalidOperationException($"Restaurant CGST + SGST exceeds the configured tax rate for rule '{restaurant.Rule.Code}'.");
            if (restaurant.Rule.IgstRatePercent > restaurant.Rule.TaxRatePercent + 0.0001m)
                throw new InvalidOperationException($"Restaurant IGST exceeds the configured tax rate for rule '{restaurant.Rule.Code}'.");
        }

        var restaurantGross = Math.Round(Math.Max(0m, restaurantAmount), 2);
        decimal restaurantTaxable;
        decimal restaurantTax;

        if (!restaurant.IsApplicable || restaurant.Rule.TaxRatePercent == 0m)
        {
            restaurantTaxable = restaurantGross;
            restaurantTax = 0m;
        }
        else if (restaurant.PricingMode == GstMode.Inclusive)
        {
            restaurantTaxable = Math.Round(
                restaurantGross * 100m / (100m + restaurant.Rule.TaxRatePercent), 2);
            restaurantTax = Math.Round(restaurantGross - restaurantTaxable, 2);
        }
        else
        {
            restaurantTaxable = restaurantGross;
            restaurantTax = Math.Round(
                restaurantTaxable * restaurant.Rule.TaxRatePercent / 100m, 2);
        }

        var platformGross = Math.Round(Math.Max(0m, platformServiceFee), 2);
        var platformTax = Math.Round(
            platformGross * platformServiceTaxRule.TaxRatePercent / 100m, 2);

        var effectiveRestaurantRate = restaurant.IsApplicable ? restaurant.Rule.TaxRatePercent : 0m;

        return new TaxBreakdown(
            effectiveRestaurantRate,
            restaurant.PricingMode,
            restaurantTaxable,
            restaurantTax,
            platformServiceTaxRule.TaxRatePercent,
            platformTax,
            restaurant.IsApplicable,
            restaurant.Rule.Id,
            restaurant.Rule.Code,
            platformServiceTaxRule.Id,
            platformServiceTaxRule.Code,
            restaurant.IsApplicable ? restaurant.Rule.CgstRatePercent : 0m,
            restaurant.IsApplicable ? restaurant.Rule.SgstRatePercent : 0m,
            restaurant.IsApplicable ? restaurant.Rule.IgstRatePercent : 0m,
            platformServiceTaxRule.CgstRatePercent,
            platformServiceTaxRule.SgstRatePercent,
            platformServiceTaxRule.IgstRatePercent);
    }
}
