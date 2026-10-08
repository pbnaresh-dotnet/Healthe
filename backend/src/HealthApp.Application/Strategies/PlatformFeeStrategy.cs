using Microsoft.Extensions.Configuration;
namespace HealthApp.Application.Strategies;

public interface IPlatformServiceFeeStrategy
{
    decimal Percent {
        get;
    }
    decimal Calculate(decimal netMealAmount);
}

public sealed class ConfigurablePlatformServiceFeeStrategy(IConfiguration configuration) : IPlatformServiceFeeStrategy
{
    public decimal Percent => configuration.GetValue<decimal?>("PlatformFees:CustomerServiceFeePercent") ?? 5m;
    public decimal Calculate(decimal netMealAmount) => Math.Round(netMealAmount * Percent / 100m, 2);
}
