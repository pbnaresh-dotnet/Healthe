using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

public sealed class HealthAppDbContext(DbContextOptions<HealthAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<SaaSPlan> SaaSPlans => Set<SaaSPlan>();
    public DbSet<OutletSubscription> OutletSubscriptions => Set<OutletSubscription>();
    public DbSet<PlatformTransaction> PlatformTransactions => Set<PlatformTransaction>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeAllergen> RecipeAllergens => Set<RecipeAllergen>();
    public DbSet<IngredientAllergen> IngredientAllergens => Set<IngredientAllergen>();
    public DbSet<CustomerAllergy> CustomerAllergies => Set<CustomerAllergy>();
    public DbSet<OutletMenuItem> OutletMenuItems => Set<OutletMenuItem>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionMealSelection> SubscriptionMealSelections => Set<SubscriptionMealSelection>();
    public DbSet<CustomerCreditTransaction> CustomerCreditTransactions => Set<CustomerCreditTransaction>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<DeliveryRouteStop> DeliveryRouteStops => Set<DeliveryRouteStop>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<CityArea> CityAreas => Set<CityArea>();
    public DbSet<OutletDeliveryArea> OutletDeliveryAreas => Set<OutletDeliveryArea>();
    public DbSet<DeliveryPricingRule> DeliveryPricingRules => Set<DeliveryPricingRule>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<SubscriptionDiscountTier> SubscriptionDiscountTiers => Set<SubscriptionDiscountTier>();
    public DbSet<MealSelectionHistory> MealSelectionHistories => Set<MealSelectionHistory>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<OrderFinancialBreakdown> OrderFinancialBreakdowns => Set<OrderFinancialBreakdown>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => HealthAppModelBuilder.Configure(modelBuilder);
}
