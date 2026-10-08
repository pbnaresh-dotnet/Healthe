using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

public sealed class HealthAppDbContext(DbContextOptions<HealthAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<OutletGroup> OutletGroups => Set<OutletGroup>();
    public DbSet<OutletBranding> OutletBrandings => Set<OutletBranding>();
    public DbSet<OutletDomain> OutletDomains => Set<OutletDomain>();
    public DbSet<SaaSPlan> SaaSPlans => Set<SaaSPlan>();
    public DbSet<OutletSubscription> OutletSubscriptions => Set<OutletSubscription>();
    public DbSet<OutletOnboardingApplication> OutletOnboardingApplications => Set<OutletOnboardingApplication>();
    public DbSet<OutletDemoRequest> OutletDemoRequests => Set<OutletDemoRequest>();
    public DbSet<PlatformTransaction> PlatformTransactions => Set<PlatformTransaction>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeAllergen> RecipeAllergens => Set<RecipeAllergen>();
    public DbSet<IngredientAllergen> IngredientAllergens => Set<IngredientAllergen>();
    public DbSet<CustomerAllergy> CustomerAllergies => Set<CustomerAllergy>();
    public DbSet<CustomerLikedMeal> CustomerLikedMeals => Set<CustomerLikedMeal>();
    public DbSet<OutletMenuItem> OutletMenuItems => Set<OutletMenuItem>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionMealSelection> SubscriptionMealSelections => Set<SubscriptionMealSelection>();
    public DbSet<CustomerCreditTransaction> CustomerCreditTransactions => Set<CustomerCreditTransaction>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<DeliveryRouteStop> DeliveryRouteStops => Set<DeliveryRouteStop>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<ServiceCity> ServiceCities => Set<ServiceCity>();
    public DbSet<CityArea> CityAreas => Set<CityArea>();
    public DbSet<OutletDeliveryArea> OutletDeliveryAreas => Set<OutletDeliveryArea>();
    public DbSet<DeliveryPricingRule> DeliveryPricingRules => Set<DeliveryPricingRule>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<SubscriptionDiscountTier> SubscriptionDiscountTiers => Set<SubscriptionDiscountTier>();
    public DbSet<MealSelectionHistory> MealSelectionHistories => Set<MealSelectionHistory>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<OrderFinancialBreakdown> OrderFinancialBreakdowns => Set<OrderFinancialBreakdown>();
    public DbSet<OutletLegalPolicyVersion> OutletLegalPolicyVersions => Set<OutletLegalPolicyVersion>();
    public DbSet<CustomerLegalAcceptance> CustomerLegalAcceptances => Set<CustomerLegalAcceptance>();
    public DbSet<ApplicationErrorLog> ApplicationErrorLogs => Set<ApplicationErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => HealthAppModelBuilder.Configure(modelBuilder);
}
