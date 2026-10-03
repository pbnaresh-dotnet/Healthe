using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
namespace HealthApp.Application.Strategies;

public sealed record PackagePricingContext(SubscriptionDuration Duration, IReadOnlyCollection<SubscriptionMealSelection> Meals);

public sealed record PackageDiscount(decimal Percent, decimal Amount);

public interface IPackageDiscountStrategy
{
    PackageDiscount Calculate(PackagePricingContext context, IReadOnlyList<SubscriptionDiscountTier>? tiers = null);
}
/// <summary>Centralized, replaceable discount policy. Tiers are intentionally configurable in one place.</summary>

public sealed class DurationAndVolumeDiscountStrategy : IPackageDiscountStrategy
{
    private static readonly decimal[,] Matrix =
    {
        {
            0m,
            0m,
            0m
        },
        {
            3m,
            3m,
            2m
        },
        {
            4m,
            5m,
            3m
        },
        {
            5m,
            6m,
            5m
        },
        {
            6m,
            7m,
            6m
        }
    };
    public PackageDiscount Calculate(PackagePricingContext context, IReadOnlyList<SubscriptionDiscountTier>? tiers = null)
    {
        var count = context.Meals.Count;
        if (tiers is {
            Count: > 0
        })
        {
            var tier = tiers.FirstOrDefault(x => count >= x.MinMeals && (!x.MaxMeals.HasValue || count <= x.MaxMeals.Value));
            if (tier is not null)
            {
                var grossConfigured = Math.Round(context.Meals.Sum(x => x.MealPrice), 2);
                var configuredPercent = context.Duration switch {
                    SubscriptionDuration.OneWeek => tier.OneWeekPercent,
                    SubscriptionDuration.TwoWeeks => tier.TwoWeeksPercent,
                    SubscriptionDuration.OneMonth => tier.OneMonthPercent,
                    _ => 0m
                };
                return new(configuredPercent, Math.Round(grossConfigured * configuredPercent / 100m, 2));
            }
        }
        var row = count switch {
            < 10 => 0,
            < 20 => 1,
            < 30 => 2,
            < 50 => 3,
            _ => 4
        };
        var column = context.Duration switch
        {
            SubscriptionDuration.OneWeek => 0,
            SubscriptionDuration.TwoWeeks => 1,
            SubscriptionDuration.OneMonth => 2,
            _ => throw new ArgumentOutOfRangeException()
        };
        var gross = Math.Round(context.Meals.Sum(x => x.MealPrice), 2);
        var percent = Matrix[row, column];
        return new(percent, Math.Round(gross * percent / 100m, 2));
    }
}

public interface ILateSkipFeePolicy
{
    decimal GetFee(DateTime nowUtc, DateTime mealDateUtc);
    bool IsLate(DateTime nowUtc, DateTime mealDateUtc);
}
/// <summary>Business rule: skipping before midnight on the delivery date is free; after midnight is ₹50.</summary>

public sealed class MidnightLateSkipFeePolicy : ILateSkipFeePolicy
{
    private const decimal Fee = 50m;
    public bool IsLate(DateTime nowUtc, DateTime mealDateUtc) => ToIndiaTime(nowUtc).Date >= mealDateUtc.Date;
    public decimal GetFee(DateTime nowUtc, DateTime mealDateUtc) => IsLate(nowUtc, mealDateUtc) ? Fee : 0m;
    private static DateTime ToIndiaTime(DateTime utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(GetIndiaZoneId());
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }
    private static string GetIndiaZoneId()
    {
        try {
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return "Asia/Kolkata";
        }
        catch {
            return "India Standard Time";
        }
    }
}

public interface IMealPriceStrategy
{
    decimal GetPrice(Recipe recipe, MealPortionSize portion);
}

public sealed class RecipeMealPriceStrategy : IMealPriceStrategy
{
    public decimal GetPrice(Recipe recipe, MealPortionSize portion) =>
    portion == MealPortionSize.Large && recipe.LargePricePerMeal > 0
    ? recipe.LargePricePerMeal
    : recipe.PricePerMeal;
}

public interface IDeliveryModeStrategy
{
    SubscriptionDeliveryMode Mode {
        get;
    }
    bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId);
}

public sealed class IndividualMealDeliveryStrategy : IDeliveryModeStrategy
{
    public SubscriptionDeliveryMode Mode => SubscriptionDeliveryMode.IndividualMealDelivery;
    public bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId) => false;
}

public sealed class OneDeliveryPerDayStrategy : IDeliveryModeStrategy
{
    public SubscriptionDeliveryMode Mode => SubscriptionDeliveryMode.OneDeliveryPerDay;
    public bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId) => existingDate.Date == mealDate.Date && existingAddressId == mealAddressId;
}

public interface IDeliveryModeStrategyFactory
{
    IDeliveryModeStrategy Create(SubscriptionDeliveryMode mode);
}

public sealed class DeliveryModeStrategyFactory(IEnumerable<IDeliveryModeStrategy> strategies) : IDeliveryModeStrategyFactory
{
    public IDeliveryModeStrategy Create(SubscriptionDeliveryMode mode) => strategies.FirstOrDefault(x => x.Mode == mode) ?? throw new InvalidOperationException($"No delivery strategy registered for {mode}.");
}
