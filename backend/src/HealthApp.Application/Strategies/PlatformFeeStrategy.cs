using Microsoft.Extensions.Configuration;
namespace HealthApp.Application.Strategies;

public interface IPlatformServiceFeeStrategy
{
    decimal Percent {
        get;
    }
    bool IncludesGatewayCosts { get; }
    decimal Calculate(decimal netMealAmount);
}

public sealed class ConfigurablePlatformServiceFeeStrategy(IConfiguration configuration) : IPlatformServiceFeeStrategy
{
    public decimal Percent
    {
        get
        {
            var value = configuration.GetValue<decimal?>("PlatformFees:CustomerServiceFeePercent") ?? 5m;
            if (value is < 0m or > 100m)
                throw new InvalidOperationException("Platform service fee must be configured between 0% and 100%.");
            return value;
        }
    }

    public bool IncludesGatewayCosts =>
        configuration.GetValue<bool?>("PlatformFees:IncludesGatewayCosts") ?? true;

    public decimal Calculate(decimal netMealAmount) => Math.Round(netMealAmount * Percent / 100m, 2);
}
