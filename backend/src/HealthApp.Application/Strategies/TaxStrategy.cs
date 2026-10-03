using Microsoft.Extensions.Configuration;

namespace HealthApp.Application.Strategies;

public sealed record TaxBreakdown(decimal RestaurantRate, decimal RestaurantAmount, decimal PlatformRate, decimal PlatformAmount);
public interface ITaxStrategy { TaxBreakdown Calculate(decimal netMealAmount, decimal platformServiceFee); }
public sealed class ConfigurableTaxStrategy(IConfiguration configuration) : ITaxStrategy
{
    public TaxBreakdown Calculate(decimal netMealAmount, decimal platformServiceFee)
    {
        var restaurantRate=configuration.GetValue<decimal?>("Tax:RestaurantGstRate")??5m;
        var platformRate=configuration.GetValue<decimal?>("Tax:PlatformServiceGstRate")??18m;
        return new(restaurantRate,Math.Round(netMealAmount*restaurantRate/100m,2),platformRate,Math.Round(platformServiceFee*platformRate/100m,2));
    }
}
