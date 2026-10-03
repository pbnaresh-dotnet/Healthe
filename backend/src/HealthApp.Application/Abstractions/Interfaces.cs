using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Abstractions;

public interface IIngredientRepository { Task<IReadOnlyList<Ingredient>> GetActiveAsync(); Task<IReadOnlyList<Ingredient>> GetByIdsAsync(IEnumerable<Guid> ids); }
public interface IAllergenRepository { Task<IReadOnlyList<Allergen>> GetActiveAsync(); Task<IReadOnlyList<Allergen>> GetByIdsAsync(IEnumerable<Guid> ids); }
public interface ICustomerAllergyRepository { Task<IReadOnlyList<CustomerAllergy>> GetByCustomerAsync(Guid customerId); Task ReplaceAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds); }
public interface ICatalogService { Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(); Task<IReadOnlyList<AllergenDto>> GetAllergensAsync(); }
public interface IAllergySafetyService { Task<IReadOnlyList<AllergyWarningDto>> GetWarningsAsync(Guid customerId, IReadOnlyCollection<Recipe> recipes); Task EnsureConfirmedAsync(Guid customerId, IReadOnlyCollection<Recipe> recipes, IReadOnlyCollection<Guid>? confirmedRecipeIds); }

public interface IUserRepository { Task<User?> FindByEmailAsync(string email); Task<User?> FindByIdAsync(Guid id); Task AddAsync(User user); Task<IReadOnlyList<User>> GetAllAsync(); }
public interface IOutletRepository { Task<IReadOnlyList<Outlet>> GetAllAsync(); Task<Outlet?> GetByIdAsync(Guid id); Task<Outlet?> GetBySlugAsync(string slug); Task<Outlet?> GetBySubdomainAsync(string subdomain); Task AddAsync(Outlet outlet); }
public interface ISaaSPlanRepository { Task<IReadOnlyList<SaaSPlan>> GetActiveAsync(); Task<SaaSPlan?> GetAsync(Guid id); }
public interface IOutletSubscriptionRepository { Task<OutletSubscription?> GetByOutletAsync(Guid outletId); Task AddAsync(OutletSubscription subscription); Task UpdateAsync(OutletSubscription subscription); }
public interface IPlatformTransactionRepository { Task AddAsync(PlatformTransaction transaction); Task<IReadOnlyList<PlatformTransaction>> GetAllAsync(); Task<bool> ExistsByReferenceAsync(string referenceId); }
public interface IMealPlanRepository { Task<IReadOnlyList<MealPlan>> GetByOutletAsync(Guid outletId); Task<MealPlan?> GetAsync(Guid id); Task AddAsync(MealPlan plan); }
public interface IRecipeRepository { Task<IReadOnlyList<Recipe>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<Recipe>> GetByOutletAndCategoryAsync(Guid outletId, string? category); Task<Recipe?> GetAsync(Guid id); Task AddAsync(Recipe recipe); Task UpdateAsync(Recipe recipe); Task DeleteAsync(Guid id); }
public interface IOutletMenuRepository { Task<IReadOnlyList<OutletMenuItem>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<OutletMenuItem>> GetByOutletDayAsync(Guid outletId, DayOfWeek day); Task AddAsync(OutletMenuItem item); Task DeleteAsync(Guid id); Task ReplaceAsync(Guid outletId, IEnumerable<OutletMenuItem> items); }
public interface ISubscriptionRepository { Task<IReadOnlyList<Subscription>> GetByCustomerAsync(Guid customerId); Task<IReadOnlyList<Subscription>> GetByOutletAsync(Guid outletId); Task<Subscription?> GetAsync(Guid id); Task AddAsync(Subscription subscription); }
public interface ISubscriptionMealSelectionRepository { Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAsync(Guid subscriptionId); Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAndDateRangeAsync(Guid subscriptionId, DateTime from, DateTime to); Task<SubscriptionMealSelection?> GetAsync(Guid id); Task AddRangeAsync(IEnumerable<SubscriptionMealSelection> selections); Task UpdateAsync(SubscriptionMealSelection selection); Task DeleteBySubscriptionAndDateRangeAsync(Guid subscriptionId, DateTime from, DateTime to); }
public interface ICustomerCreditRepository { Task<decimal> GetBalanceAsync(Guid customerId); Task<IReadOnlyList<CustomerCreditTransaction>> GetTransactionsAsync(Guid customerId); Task AddAsync(CustomerCreditTransaction transaction); }
public interface IOrderRepository { Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId); Task<IReadOnlyList<Order>> GetByOutletAsync(Guid outletId); Task<Order?> GetBySubscriptionAsync(Guid subscriptionId); Task AddAsync(Order order); Task UpdateAsync(Order order); }
public interface IDeliveryRepository { Task<IReadOnlyList<Delivery>> GetByOutletAsync(Guid outletId); Task AddAsync(Delivery delivery); Task<Delivery?> GetAsync(Guid id); Task UpdateAsync(Delivery delivery); Task<IReadOnlyList<Delivery>> GetBySubscriptionAsync(Guid subscriptionId); }
public interface ITokenService { AuthResponse CreateToken(User user); }
public interface IPasswordService { string Hash(string password); bool Verify(string password, string hash); }
public interface ICurrentUser { Guid? UserId { get; } Guid? OutletId { get; } string? Role { get; } bool IsAuthenticated { get; } }
public interface IAuthService { Task<AuthResponse?> LoginAsync(LoginRequest request); Task<AuthResponse> RegisterAsync(RegisterRequest request); }
public interface IMarketplaceService { Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync(); Task<AvailabilityResponse> GetAvailabilityAsync(double latitude, double longitude); Task<IReadOnlyList<OutletDto>> GetAllOutletsAsync(); Task<OutletDto?> GetOutletAsync(string slug); Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(Guid outletId); Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(Guid outletId, string? category); Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(Guid outletId); }
public interface ICustomerService { Task<UserDto?> GetProfileAsync(); Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync(); Task<IReadOnlyList<OrderDto>> GetOrdersAsync(); Task<SubscriptionDto?> SubscribeAsync(CreateSubscriptionRequest request); Task<SubscriptionQuoteDto?> QuoteAsync(SubscriptionQuoteRequest request); Task<IReadOnlyList<RecipeDto>> GetSubscriptionRecipesAsync(Guid subscriptionId, string? category); Task<IReadOnlyList<MenuItemDto>> GetSubscriptionMenuAsync(Guid subscriptionId); Task<IReadOnlyList<MealSelectionDto>> GetMealSelectionsAsync(Guid subscriptionId, DateTime? weekStart); Task<IReadOnlyList<MealSelectionDto>> SaveMealSelectionsAsync(Guid subscriptionId, SaveMealSelectionsRequest request); Task<MealSelectionDto?> SkipMealAsync(Guid subscriptionId, Guid selectionId, SkipMealRequest request); Task<IReadOnlyList<MealSelectionDto>> SkipDayAsync(Guid subscriptionId, DateTime date, SkipDayRequest request); Task<MealSelectionDto?> RescheduleMealAsync(Guid subscriptionId, Guid selectionId, RescheduleMealRequest request); Task<CreditBalanceDto> GetCreditBalanceAsync(); Task<IReadOnlyList<CreditTransactionDto>> GetCreditTransactionsAsync(); }
public interface IOutletService { Task<object> GetDashboardAsync(); Task<OutletSubscriptionDetailDto?> GetSubscriptionDetailAsync(Guid subscriptionId); Task<OutletKitchenDayDto> GetKitchenDayAsync(DateTime date); Task<OutletBillingDto?> GetBillingAsync(); Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync(); Task<OutletBillingDto?> ChangeSubscriptionAsync(ChangeOutletSubscriptionRequest request); Task<OutletDto?> GetCurrentAsync(); Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(); Task<MealPlanDto?> CreatePlanAsync(CreateMealPlanRequest request); Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(string? category); Task<RecipeDto?> CreateRecipeAsync(CreateRecipeRequest request); Task<RecipeDto?> UpdateRecipeAsync(Guid recipeId, UpdateRecipeRequest request); Task<bool> DeleteRecipeAsync(Guid recipeId); Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(); Task<IReadOnlyList<MenuItemDto>> SaveMenuAsync(BulkMenuRequest request); Task<IReadOnlyList<UserDto>> GetCustomersAsync(); Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync(); Task<IReadOnlyList<OrderDto>> GetOrdersAsync(); Task<IReadOnlyList<DeliveryDto>> GetDeliveriesAsync(); }
public interface IAdminService { Task<IReadOnlyList<OutletDto>> GetOutletsAsync(); Task<IReadOnlyList<UserDto>> GetUsersAsync(); Task<object> GetDashboardAsync(); Task<PlatformRevenueDto> GetRevenueAsync(); }


public interface ICustomerProfileRepository { Task<CustomerProfile?> GetAsync(Guid customerId); Task AddOrUpdateAsync(CustomerProfile profile); }
public interface ICityAreaRepository { Task<IReadOnlyList<CityArea>> GetActiveAsync(string? city = null); Task<CityArea?> GetAsync(Guid id); Task AddAsync(CityArea area); }
public interface IOutletDeliveryAreaRepository { Task<IReadOnlyList<OutletDeliveryArea>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<CityArea>> GetAreasForOutletAsync(Guid outletId); Task ReplaceAsync(Guid outletId, IEnumerable<OutletDeliveryArea> areas); }
public interface IDeliveryPricingRepository { Task<IReadOnlyList<DeliveryPricingRule>> GetByOutletAsync(Guid outletId); Task AddAsync(DeliveryPricingRule rule); Task DeleteAsync(Guid id, Guid outletId); }
public interface ICustomerAddressRepository { Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId); Task<CustomerAddress?> GetAsync(Guid customerId, Guid id); Task AddAsync(CustomerAddress address); Task UpdateAsync(CustomerAddress address); Task DeleteAsync(Guid customerId, Guid id); }
public interface ISubscriptionDiscountTierRepository { Task<IReadOnlyList<SubscriptionDiscountTier>> GetByOutletAsync(Guid outletId); Task AddAsync(SubscriptionDiscountTier tier); Task UpdateAsync(SubscriptionDiscountTier tier); Task DeleteAsync(Guid outletId, Guid id); }
public interface IMealSelectionHistoryRepository { Task AddAsync(MealSelectionHistory history); Task<IReadOnlyList<MealSelectionHistory>> GetBySelectionAsync(Guid selectionId); }
public interface IPaymentTransactionRepository { Task<PaymentTransaction?> GetAsync(Guid id); Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string key); Task AddAsync(PaymentTransaction payment); Task UpdateAsync(PaymentTransaction payment); }
public interface IDiscountCodeRepository { Task<DiscountCode?> GetAsync(Guid? outletId, string code); Task<IReadOnlyList<DiscountCode>> GetByOutletAsync(Guid outletId); Task AddAsync(DiscountCode code); Task UpdateAsync(DiscountCode code); }
public interface IOrderFinancialRepository { Task AddAsync(OrderFinancialBreakdown breakdown); Task<OrderFinancialBreakdown?> GetByOrderAsync(Guid orderId); }
public interface IDeliveryCalculator { Task<DeliveryQuoteDto> QuoteAsync(Guid outletId, Guid customerId, Guid addressId); Task<decimal> CalculateForSelectionsAsync(Guid outletId, Guid customerId, SubscriptionDeliveryMode mode, IReadOnlyList<SubscriptionMealSelection> selections); }

public interface ICustomerAddressService { Task<IReadOnlyList<CustomerAddressDto>> GetAsync(); Task<CustomerAddressDto?> CreateAsync(CreateCustomerAddressRequest request); Task<CustomerAddressDto?> UpdateAsync(Guid id, UpdateCustomerAddressRequest request); Task<bool> DeleteAsync(Guid id); Task<IReadOnlyList<DeliveryQuoteDto>> QuoteAsync(Guid outletId); }
public interface ICustomerProfileService { Task<CustomerProfileDto?> GetAsync(); Task<CustomerProfileDto?> SaveAsync(SaveCustomerProfileRequest request); }
public interface IOutletDeliveryService { Task<IReadOnlyList<CityAreaDto>> GetAvailableAreasAsync(string? city); Task<IReadOnlyList<OutletDeliveryAreaDto>> GetAreasAsync(); Task<IReadOnlyList<DeliveryPricingRuleDto>> GetPricingAsync(); Task<IReadOnlyList<OutletDeliveryAreaDto>> SaveAreasAsync(SaveOutletDeliveryAreasRequest request); Task<DeliveryPricingRuleDto?> AddPricingAsync(CreateDeliveryPricingRuleRequest request); Task<bool> DeletePricingAsync(Guid id); }
public interface IDiscountConfigurationService { Task<IReadOnlyList<SubscriptionDiscountTierDto>> GetTiersAsync(); Task<SubscriptionDiscountTierDto?> AddTierAsync(SaveSubscriptionDiscountTierRequest request); Task<SubscriptionDiscountTierDto?> UpdateTierAsync(Guid id, SaveSubscriptionDiscountTierRequest request); Task<bool> DeleteTierAsync(Guid id); }
public interface ICityAreaAdminService { Task<IReadOnlyList<CityAreaDto>> GetAsync(string? city); Task<CityAreaDto?> CreateAsync(CreateCityAreaRequest request); }
public interface IPaymentService { Task<PaymentDto?> CreateAsync(CreatePaymentRequest request); Task<PaymentDto?> GetAsync(Guid id); }
public interface IDeliveryLabelService { Task<IReadOnlyList<DeliveryLabelDto>> GetLabelsAsync(DateTime? date); }
public interface IDeliveryRouteRepository
{
    Task<IReadOnlyList<DeliveryRoute>> GetByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot);
    Task DeleteByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot);
    Task AddAsync(DeliveryRoute route);
}
public record RouteOptimizationStop(Guid Id, double Latitude, double Longitude);
public record RouteOptimizationResult(double DistanceKm, double DurationMinutes, IReadOnlyList<Guid> OrderedStopIds, IReadOnlyList<IReadOnlyList<double>> Geometry);
public interface IRouteOptimizationService
{
    Task<RouteOptimizationResult> OptimizeAsync(double outletLatitude, double outletLongitude, IReadOnlyList<RouteOptimizationStop> stops, CancellationToken cancellationToken = default);
}
public interface IDeliveryRouteService
{
    Task<IReadOnlyList<DriverDto>> GetDriversAsync();
    Task<DriverDto?> CreateDriverAsync(CreateDriverRequest request);
    Task<DeliveryRoutePlanDto> GetPlanAsync(DateTime date);
    Task<DeliveryRoutePlanDto> PlanRoutesAsync(PlanDeliveryRoutesRequest request);
}
public interface IOutletDiscountCodeService { Task<IReadOnlyList<DiscountCodeDto>> GetAsync(); Task<DiscountCodeDto?> CreateAsync(CreateDiscountCodeRequest request); Task<bool> DisableAsync(Guid id); }
