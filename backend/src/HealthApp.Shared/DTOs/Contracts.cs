namespace HealthApp.Shared.DTOs;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role = "Customer", string? OutletSlug = null);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
public record UserDto(Guid Id, string Email, string FirstName, string LastName, string Role, Guid? OutletId);
public record OutletDto(Guid Id, string Name, string Slug, string Subdomain, string City, string State, string Pincode, string Status, string BillingPlan, string LogoUrl, string HeroImageUrl, IReadOnlyList<string> HealthHighlights, string PrimaryColor, bool IsAvailable, double DistanceKm);
public record SaaSPlanDto(Guid Id, string Name, decimal MonthlyFee, decimal AnnualFee, int IncludedActiveCustomers, decimal AdditionalCustomerFee, decimal CustomerTransactionFeePercent, string Description, bool IsActive);
public record OutletBillingDto(Guid OutletId, Guid SaaSPlanId, string PlanName, string BillingCycle, decimal SubscriptionFee, decimal SetupFee, decimal TransactionFeePercent, int ActiveCustomers, int IncludedActiveCustomers, decimal AdditionalCustomerFee, decimal EstimatedAdditionalCustomerFee, DateTime RenewalDate, string Status);
public record PlatformRevenueDto(decimal OutletSubscriptionRevenue, decimal CustomerTransactionRevenue, decimal TotalRevenue, decimal LateSkipFeeRevenue = 0m, decimal CustomerServiceFeeRevenue = 0m, decimal OutletCommissionRevenue = 0m);
public record MealPlanDto(Guid Id, Guid OutletId, string Name, string Frequency, int MealsPerDay, int MealsPerWeek, decimal Price, string Currency, string Description, bool IsActive);
public record IngredientDto(Guid Id, string Name, string DefaultUnit);
public record AllergenDto(Guid Id, string Name);
public record RecipeIngredientInput(Guid IngredientId, decimal Quantity, string Unit);
public record RecipeIngredientDto(Guid IngredientId, string Name, decimal Quantity, string Unit, IReadOnlyList<AllergenDto> Allergens);
public record AllergyWarningDto(Guid RecipeId, string RecipeName, IReadOnlyList<string> MatchedAllergies, IReadOnlyList<string> MatchedIngredients, string Message);
public record RecipeDto(Guid Id, Guid OutletId, string Name, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, string Category, decimal PricePerMeal, decimal LargePricePerMeal, string Description, string ImageUrl, string Tags, bool IsActive, IReadOnlyList<RecipeIngredientDto> Ingredients, IReadOnlyList<AllergenDto> Allergens);
public record MenuItemDto(Guid Id, Guid OutletId, Guid RecipeId, string RecipeName, DayOfWeek DayOfWeek, string MealSlot, int MealSlotValue, decimal PricePerMeal, decimal LargePricePerMeal, int Calories, int ProteinGrams, string Category, string ImageUrl, bool IsAvailable, int DisplayOrder);
public record SubscriptionDto(Guid Id, Guid CustomerId, Guid OutletId, Guid MealPlanId, string PlanName, string DeliveryMode, decimal Price, decimal DeliveryFee, decimal CustomerTransactionFeePercent, decimal TransactionFee, decimal TotalCharged, decimal OutletAmount, string Frequency, int MealsPerDay, int MealsPerWeek, string Status, DateTime NextDeliveryDate, decimal AvailableCredit, string PaymentStatus = "Pending");
public record CustomerDashboardDeliveryDto(Guid DeliveryId, Guid SubscriptionId, DateTime ScheduledDate, string MealSlot, string DeliveryWindow, string Status, string Address, double Latitude, double Longitude, int MealCount);
public record CustomerDashboardMealDto(Guid SelectionId, Guid SubscriptionId, DateTime MealDate, string MealSlot, string RecipeName, string Category, string ImageUrl, int Calories, int ProteinGrams, string Status, decimal MealPrice, Guid? DeliveryId);
public record CustomerDashboardSubscriptionDto(Guid Id, string PlanName, string DeliveryMode, int MealsPerWeek, decimal TotalCharged, DateTime NextDeliveryDate, string Status, string PaymentStatus);
public record CustomerDashboardBenefitsDto(int MealsThisWeek, int ProteinGramsThisWeek, int CaloriesThisWeek, decimal SubscriptionSavings, int DeliveryDaysThisWeek, int ActiveSubscriptions);
public record CustomerDashboardDto(IReadOnlyList<CustomerDashboardDeliveryDto> TodayDeliveries, IReadOnlyList<CustomerDashboardMealDto> TodayMeals, IReadOnlyList<CustomerDashboardSubscriptionDto> ActiveSubscriptions, CustomerDashboardBenefitsDto Benefits);
public record OrderDto(Guid Id, Guid CustomerId, Guid OutletId, decimal Total, string Status, DateTime DeliveryDate, string Address);
public record DeliveryDto(Guid Id, Guid OrderId, Guid OutletId, string CustomerName, string Address, DateTime ScheduledDate, string MealSlot, decimal DeliveryFee, string Status);
public record DriverDto(Guid Id, string Name, string Email, bool IsActive);
public record CreateDriverRequest(string FirstName, string LastName, string Email, string Password);
public record PlanDeliveryRoutesRequest(DateTime Date, IReadOnlyList<Guid> DriverIds, int MealSlot = 2);
public record DeliveryMapPointDto(Guid DeliveryId, Guid CustomerId, string CustomerName, Guid AddressId, string Address, double Latitude, double Longitude, int DeliveryCount, string MealSlot, string Status, Guid? RouteId, int? StopSequence);
public record DeliveryRouteStopDto(Guid Id, int StopSequence, string MealSlot, Guid AddressId, Guid CustomerId, string CustomerName, string Address, double Latitude, double Longitude, int DeliveryCount, string Status, IReadOnlyList<Guid> DeliveryIds);
public record DeliveryRouteDto(Guid Id, Guid DriverId, string DriverName, DateTime DeliveryDate, string MealSlot, string DeliveryWindow, string Status, double TotalDistanceKm, double TotalDurationMinutes, string RoutingSource, IReadOnlyList<DeliveryRouteStopDto> Stops, IReadOnlyList<IReadOnlyList<double>> Geometry);
public record DeliveryRoutePlanDto(DateTime Date, string OutletName, double OutletLatitude, double OutletLongitude, int TotalDeliveryPoints, int TotalDeliveries, int UnassignedPoints, double PlannedDistanceKm, double PlannedDurationMinutes, string OptimizationSource, IReadOnlyList<DeliveryMapPointDto> Points, IReadOnlyList<DeliveryRouteDto> Routes);
public record CreateSubscriptionRequest(Guid OutletId, string DeliveryMode, string Frequency, IReadOnlyList<MealSelectionItem> Selections, string Duration = "OneWeek", string? DiscountCode = null, IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null);
public record CreateMealPlanRequest(string Name, string Frequency, int MealsPerDay, decimal Price, string Description);
public record CreateRecipeRequest(string Name, string Category, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, decimal PricePerMeal, decimal LargePricePerMeal, string Description = "", string ImageUrl = "", string Tags = "", IReadOnlyList<RecipeIngredientInput>? Ingredients = null, IReadOnlyList<Guid>? AllergenIds = null);
public record UpdateRecipeRequest(string Name, string Category, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, decimal PricePerMeal, decimal LargePricePerMeal, string Description = "", string ImageUrl = "", string Tags = "", IReadOnlyList<RecipeIngredientInput>? Ingredients = null, IReadOnlyList<Guid>? AllergenIds = null, bool IsActive = true);
public record MealSelectionItem(DateTime MealDate, int MealSlot, Guid RecipeId, int PortionSize = 1, Guid? AddressId = null);
public record SaveMealSelectionsRequest(IReadOnlyList<MealSelectionItem> Selections, IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null);
public record MealSelectionDto(Guid Id, Guid SubscriptionId, DateTime MealDate, int MealSlot, Guid RecipeId, string RecipeName, string Category, int PortionSize, string Status, decimal MealPrice, decimal DeliveryFee, decimal LateSkipFee = 0m, DateTime? SkippedAtUtc = null, DateTime? RescheduledAtUtc = null);
public record SkipMealRequest(string Reason = "Customer skipped meal");
public record SkipDayRequest(string Reason = "Customer skipped day");
public record RescheduleMealRequest(DateTime NewMealDate, int NewMealSlot, Guid? AddressId = null);
public record CreditBalanceDto(decimal Balance);
public record CreditTransactionDto(Guid Id, decimal Amount, string Type, string Reason, DateTime CreatedAt);
public record AvailabilityResponse(bool ServiceAvailable, string Message, IReadOnlyList<OutletDto> Outlets);
public record ChangeOutletSubscriptionRequest(Guid SaaSPlanId, string BillingCycle = "Monthly");
public record SaveMenuItemRequest(Guid RecipeId, DayOfWeek DayOfWeek, int MealSlot, bool IsAvailable = true, int DisplayOrder = 0);
public record BulkMenuRequest(IReadOnlyList<SaveMenuItemRequest> Items);

public record CustomerProfileDto(Guid Id, Guid CustomerId, decimal? WeightKg, decimal? HeightCm, decimal? Bmi, string Goal, string ActivityLevel, string Diet, DateTime UpdatedAtUtc, IReadOnlyList<AllergenDto> Allergies);
public record SaveCustomerProfileRequest(decimal? WeightKg, decimal? HeightCm, DateTime? DateOfBirth, string Goal, string ActivityLevel, string Diet, IReadOnlyList<Guid>? AllergyIds = null);
public record CityAreaDto(Guid Id, string City, string State, string Name, string Pincode, double Latitude, double Longitude, bool IsActive);
public record CityDto(string City, string State, int AreaCount);
public record ReverseGeocodeDto(string? HouseNumber, string? Road, string? Suburb, string? Neighbourhood, string? City, string? State, string? Pincode, string? Country, string? DisplayName);
public record CreateCityAreaRequest(string City, string State, string Name, string Pincode, double Latitude, double Longitude);
public record OutletDeliveryAreaDto(Guid Id, Guid OutletId, Guid CityAreaId, string AreaName, string City, string Pincode, bool IsActive);
public record SaveOutletDeliveryAreasRequest(IReadOnlyList<Guid> CityAreaIds);
public record DeliveryPricingRuleDto(Guid Id, Guid OutletId, decimal MaxDistanceKm, decimal Fee, bool IsActive);
public record CreateDeliveryPricingRuleRequest(decimal MaxDistanceKm, decimal Fee);
public record CustomerAddressDto(Guid Id, string Label, string AreaName, string City, string Pincode, string AddressLine1, string AddressLine2, string ContactName, string ContactPhone, double Latitude, double Longitude, bool IsDefault);
public record CreateCustomerAddressRequest(Guid CityAreaId, string Label, string AddressLine1, string AddressLine2, string ContactName, string ContactPhone, double Latitude, double Longitude, bool IsDefault = false);
public record UpdateCustomerAddressRequest(Guid CityAreaId, string Label, string AddressLine1, string AddressLine2, string ContactName, string ContactPhone, double Latitude, double Longitude, bool IsDefault = false);
public record DeliveryQuoteDto(Guid AddressId, double DistanceKm, decimal DeliveryFee, string AreaName);
public record SubscriptionQuoteRequest(Guid OutletId, string DeliveryMode, string Duration, IReadOnlyList<MealSelectionItem> Selections, string? DiscountCode = null, IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null);
public record SubscriptionQuoteDto(decimal GrossMealAmount, decimal SubscriptionDiscountPercent, decimal SubscriptionDiscountAmount, decimal NetMealAmount, decimal RestaurantGstAmount, decimal DeliveryFee, decimal PlatformServiceFee, decimal PlatformServiceGst, decimal TotalCharged, decimal OutletCommissionPercent, decimal OutletCommissionAmount, decimal HealthAppRevenue, IReadOnlyList<DeliveryQuoteDto> DeliveryQuotes, IReadOnlyList<AllergyWarningDto> AllergyWarnings, bool RequiresAllergyConfirmation);
public record SubscriptionDiscountTierDto(Guid Id, Guid OutletId, int MinMeals, int? MaxMeals, decimal OneWeekPercent, decimal TwoWeeksPercent, decimal OneMonthPercent, bool IsActive);
public record SaveSubscriptionDiscountTierRequest(int MinMeals, int? MaxMeals, decimal OneWeekPercent, decimal TwoWeeksPercent, decimal OneMonthPercent, bool IsActive = true);
public record DiscountCodeDto(Guid Id, Guid? OutletId, string Code, decimal Percent, decimal? MaxAmount, int? MaxRedemptions, int RedemptionCount, DateTime? StartsAtUtc, DateTime? EndsAtUtc, bool IsActive);
public record CreateDiscountCodeRequest(string Code, decimal Percent, decimal? MaxAmount, int? MaxRedemptions, DateTime? StartsAtUtc, DateTime? EndsAtUtc, bool IsActive = true);
public record DeliveryLabelDto(Guid SubscriptionId, Guid SelectionId, DateTime MealDate, int MealSlot, string MealSlotName, string DeliveryWindow, string OutletName, string LogoUrl, string CustomerName, string CustomerPhone, string MealName, string Category, string PortionSize, string SubscriptionPlanName, string AddressLabel, string Address, string AreaName, string Pincode, decimal MealPrice, decimal DeliveryFee);
public record OutletSubscriptionMealDto(Guid SelectionId, DateTime MealDate, int MealSlot, string MealSlotName, string DeliveryWindow, Guid RecipeId, string MealName, string Category, string PortionSize, decimal MealPrice, decimal DeliveryFee, string AddressLabel, string Address, string AreaName, string Pincode, string ContactPhone, string Status);
public record OutletSubscriptionDetailDto(Guid Id, Guid CustomerId, string CustomerName, string CustomerEmail, string PlanName, string DeliveryMode, string Duration, DateTime StartDate, DateTime EndDate, string Frequency, int MealsPerDay, int MealsPerWeek, decimal MealAmount, decimal DiscountAmount, decimal RestaurantGstAmount, decimal PackageAmountWithGst, decimal DeliveryFee, string Status, IReadOnlyList<OutletSubscriptionMealDto> Meals);
public record OutletDashboardSubscriptionDto(Guid Id, Guid CustomerId, string CustomerName, string PlanName, DateTime StartDate, DateTime EndDate, int MealCount, string Status);
public record OutletDashboardDeliverySlotDto(string MealSlot, string DeliveryWindow, int DeliveryJobs, int PendingJobs, int CompletedJobs);
public record OutletDashboardDto(
    OutletDto Outlet,
    int MealPlans,
    int Recipes,
    int ActiveCustomers,
    int ActiveSubscriptions,
    int NewCustomers,
    int NewSubscriptions,
    int Orders7d,
    decimal Sales7d,
    int TodayMealBoxes,
    int TodayDeliveryJobs,
    int TodayDeliveryPoints,
    int TodayPendingDeliveries,
    IReadOnlyList<OutletDashboardDeliverySlotDto> TodayDeliverySlots,
    IReadOnlyList<OutletDashboardSubscriptionDto> RecentSubscriptions);
public record OutletKitchenMealCountDto(string MealName, string Category, string PortionSize, int Quantity);
public record OutletKitchenDayDto(DateTime Date, string OutletName, string OutletLogoUrl, int TotalMeals, int UniqueCustomers, int ActiveSubscriptions, IReadOnlyList<OutletKitchenMealCountDto> Production, IReadOnlyList<DeliveryLabelDto> Labels);
public record PaymentDto(Guid Id, Guid? SubscriptionId, string Provider, string ProviderPaymentId, decimal Amount, string Currency, string Status, DateTime CreatedAtUtc, DateTime? PaidAtUtc);
public record CreatePaymentRequest(Guid SubscriptionId, string IdempotencyKey, string Provider = "Mock");
public record RescheduleUnusedMealRequest(DateTime NewMealDate, int NewMealSlot, Guid? AddressId = null);
