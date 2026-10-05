using HealthApp.Domain.Enums;
using Microsoft.Extensions.Configuration;
namespace HealthApp.Application.Strategies;

public sealed record TaxBreakdown(
    decimal RestaurantRate,
    GstMode RestaurantMode,
    decimal RestaurantTaxableAmount,
    decimal RestaurantAmount,
    decimal PlatformRate,
    decimal PlatformAmount);

public interface ITaxStrategy
{
    TaxBreakdown Calculate(decimal restaurantAmount, decimal platformServiceFee, decimal restaurantRate, GstMode restaurantMode);
}

public sealed class ConfigurableTaxStrategy(IConfiguration configuration) : ITaxStrategy
{
    public TaxBreakdown Calculate(decimal restaurantAmount, decimal platformServiceFee, decimal restaurantRate, GstMode restaurantMode)
    {
        var configuredRate = restaurantRate >= 0m ? restaurantRate : (configuration.GetValue<decimal?>("Tax:RestaurantGstRate") ?? 5m);
        var platformRate = configuration.GetValue<decimal?>("Tax:PlatformServiceGstRate") ?? 18m;

        if (configuredRate > 100m) throw new ArgumentOutOfRangeException(nameof(restaurantRate), "Restaurant GST rate cannot exceed 100%.");
        if (platformRate < 0m || platformRate > 100m) throw new ArgumentOutOfRangeException(nameof(platformRate), "Platform GST rate must be between 0% and 100%.");

        var restaurantGross = Math.Round(Math.Max(0m, restaurantAmount), 2);
        var restaurantTaxable = restaurantMode == GstMode.Inclusive
            ? Math.Round(restaurantGross * 100m / (100m + configuredRate), 2)
            : restaurantGross;
        var restaurantTax = restaurantMode == GstMode.Inclusive
            ? Math.Round(restaurantGross - restaurantTaxable, 2)
            : Math.Round(restaurantTaxable * configuredRate / 100m, 2);

        var platformTax = Math.Round(Math.Max(0m, platformServiceFee) * platformRate / 100m, 2);
        return new(configuredRate, restaurantMode, restaurantTaxable, restaurantTax, platformRate, platformTax);
    }
}