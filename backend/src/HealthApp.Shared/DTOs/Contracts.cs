namespace HealthApp.Shared.DTOs;

public record LoginRequest(string Email, string Password, string? OutletSlug = null);
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role = "Customer", string? OutletSlug = null, string? MobileNumber = null, Guid? LegalPolicyVersionId = null, bool LegalAccepted = false, bool MarketingOptIn = false);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
public record UserDto(Guid Id, string Email, string FirstName, string LastName, string Role, Guid? OutletId, bool IsDemo = false, DateTime? DemoExpiresAtUtc = null, string? MobileNumber = null, bool MarketingOptIn = false, DateTime? MarketingOptInAtUtc = null);
public record OutletDto(Guid Id, string Name, string Slug, string Subdomain, string City, string State, string Pincode, string Status, string BillingPlan, string LogoUrl, string HeroImageUrl, IReadOnlyList<string> HealthHighlights, string PrimaryColor, bool IsAvailable, double DistanceKm, double Rating = 4.8, int ReviewCount = 0, string About = "Fresh, healthy meals prepared with quality ingredients.", double Latitude = 0, double Longitude = 0, string Tagline = "", string SecondaryColor = "", string FaviconUrl = "", string DeliveryCoverageMode = "Radius", double ServiceRadiusKm = 20, string FontFamily = "Inter", string ThemeStyle = "Fresh", string ButtonStyle = "Rounded", string CardStyle = "Soft", string CustomPackagePricingMode = "Calculated", bool ShowPackagePriceToCustomer = true, bool ShowDeliveryFeeToCustomer = true);
public record SaaSPlanDto(Guid Id, string Name, decimal MonthlyFee, decimal AnnualFee, int IncludedActiveCustomers, decimal AdditionalCustomerFee, decimal CustomerTransactionFeePercent, string Description, bool IsActive);
public record OutletBrandingDto(
    string BrandName,
    string Tagline,
    string LogoUrl,
    string HeroImageUrl,
    string FaviconUrl,
    string PrimaryColor,
    string SecondaryColor,
    IReadOnlyList<string> HealthHighlights,
    string About,
    string FooterText,
    string FontFamily = "Inter",
    string ThemeStyle = "Fresh",
    string ButtonStyle = "Rounded",
    string CardStyle = "Soft");

public record UpdateOutletBrandingRequest(
    string BrandName,
    string Tagline,
    string PrimaryColor,
    string SecondaryColor,
    string HealthHighlights,
    string About,
    string FooterText,
    string FontFamily = "Inter",
    string ThemeStyle = "Fresh",
    string ButtonStyle = "Rounded",
    string CardStyle = "Soft");

public record OutletSettingsDto(
    Guid OutletId,
    string OutletName,
    string City,
    string State,
    string Pincode,
    string DeliveryDays,
    decimal RestaurantGstRate,
    string RestaurantGstMode,
    OutletReadinessDto Readiness,
    OutletBrandingDto? Branding = null,
    string DeliveryCoverageMode = "Radius",
    double ServiceRadiusKm = 20,
    double Latitude = 0,
    double Longitude = 0,
    string OutletSlug = "",
    string CustomPackagePricingMode = "Calculated",
    bool ShowPackagePriceToCustomer = true,
    bool ShowDeliveryFeeToCustomer = true);

public record OutletLegalPolicyVersionDto(
    Guid Id,
    string Version,
    DateTime EffectiveDateUtc,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    bool IsPublished,
    string ContentHash);

public record OutletLegalPoliciesDto(
    Guid OutletId,
    string OutletName,
    string CustomerTermsAndConditions,
    string CustomerPrivacyPolicy,
    string CancellationRefundPolicy,
    string MealSkipReschedulePolicy,
    string DeliveryPolicy,
    string AllergenDietaryDisclaimer,
    string PaymentPricingPromotionalTerms,
    string LegalVersion,
    DateTime? LegalEffectiveDateUtc,
    bool LegalPoliciesPublished,
    Guid? PublishedVersionId = null,
    string PublishedVersion = "",
    DateTime? PublishedEffectiveDateUtc = null,
    IReadOnlyList<OutletLegalPolicyVersionDto>? VersionHistory = null);

public record UpdateOutletLegalPoliciesRequest(
    string CustomerTermsAndConditions,
    string CustomerPrivacyPolicy,
    string CancellationRefundPolicy,
    string MealSkipReschedulePolicy,
    string DeliveryPolicy,
    string AllergenDietaryDisclaimer,
    string PaymentPricingPromotionalTerms,
    string LegalVersion = "1.0",
    DateTime? LegalEffectiveDateUtc = null,
    bool LegalPoliciesPublished = false);



public record UpdateOutletSettingsRequest(string DeliveryDays, string DeliveryCoverageMode = "Radius", double? ServiceRadiusKm = null);
public record UpdateOutletPackageSettingsRequest(string CustomPackagePricingMode = "Calculated", bool ShowPackagePriceToCustomer = true, bool ShowDeliveryFeeToCustomer = true);

public record OutletReadinessItemDto(
    string Key,
    string Title,
    string Description,
    bool IsComplete,
    int CurrentCount,
    int RequiredCount,
    string ConfigureSection);

public record OutletReadinessDto(
    string Status,
    bool IsLive,
    bool CanGoLive,
    IReadOnlyList<OutletReadinessItemDto> Items,
    string Message);

public record OutletBillingDto(Guid OutletId, Guid SaaSPlanId, string PlanName, string BillingCycle, decimal SubscriptionFee, decimal SetupFee, decimal TransactionFeePercent, int ActiveCustomers, int IncludedActiveCustomers, decimal AdditionalCustomerFee, decimal EstimatedAdditionalCustomerFee, DateTime RenewalDate, string Status);
public record OutletTaxSettingsDto(decimal RestaurantGstRate, string RestaurantGstMode);
public record UpdateOutletTaxSettingsRequest(decimal RestaurantGstRate, string RestaurantGstMode);
public record PlatformRevenueDto(decimal OutletSubscriptionRevenue, decimal CustomerTransactionRevenue, decimal TotalRevenue, decimal LateSkipFeeRevenue = 0m, decimal CustomerServiceFeeRevenue = 0m, decimal OutletCommissionRevenue = 0m);
public record MealPlanDto(Guid Id, Guid OutletId, string Name, string Frequency, int MealsPerDay, int MealsPerWeek, decimal Price, string Currency, string Description, bool IsActive, bool IsPreplanned = false, string AvailableCity = "", int DurationDays = 7);
public record IngredientDto(Guid Id, string Name, string DefaultUnit);
public record AllergenDto(Guid Id, string Name);
public record CustomerLikedMealDto(Guid RecipeId, string RecipeName, string ImageUrl, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, string Category, decimal PricePerMeal, int FiberGrams = 0);
public record RecipeIngredientInput(Guid IngredientId, decimal Quantity, string Unit);
public record RecipeIngredientDto(Guid IngredientId, string Name, decimal Quantity, string Unit, IReadOnlyList<AllergenDto> Allergens);
public record AllergyWarningDto(Guid RecipeId, string RecipeName, IReadOnlyList<string> MatchedAllergies, IReadOnlyList<string> MatchedIngredients, string Message);
public record RecipeDto(Guid Id, Guid OutletId, string Name, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, string Category, decimal PricePerMeal, decimal LargePricePerMeal, string Description, string ImageUrl, string Tags, bool IsActive, IReadOnlyList<RecipeIngredientDto> Ingredients, IReadOnlyList<AllergenDto> Allergens, int FiberGrams = 0, string MealType = "Meal");
public record MenuItemDto(Guid Id, Guid OutletId, Guid RecipeId, string RecipeName, DayOfWeek DayOfWeek, string MealSlot, int MealSlotValue, decimal PricePerMeal, decimal LargePricePerMeal, int Calories, int ProteinGrams, string Category, string ImageUrl, bool IsAvailable, int DisplayOrder, int CarbsGrams = 0, int FatGrams = 0, int FiberGrams = 0, string OptionGroup = "Main", bool IsRequired = true, int MaxSelections = 1, string MealType = "Meal");
public record SubscriptionDto(Guid Id, Guid CustomerId, Guid OutletId, Guid MealPlanId, string PlanName, string DeliveryMode, decimal Price, decimal DeliveryFee, decimal CustomerTransactionFeePercent, decimal TransactionFee, decimal TotalCharged, decimal OutletAmount, string Frequency, int MealsPerDay, int MealsPerWeek, string Status, DateTime NextDeliveryDate, decimal AvailableCredit, string PaymentStatus = "Pending", string DeliveryCity = "", decimal GrossMealAmount = 0m, decimal DiscountAmount = 0m, decimal RestaurantTaxableAmount = 0m, decimal RestaurantGstAmount = 0m, decimal RestaurantGstRate = 0m, string RestaurantGstMode = "Exclusive", decimal PlatformServiceFee = 0m, decimal PlatformServiceGst = 0m, string PackageStatus = "Active", bool IsOutletCreated = false, string OutletDiscountType = "None", decimal OutletDiscountValue = 0m, string OutletDiscountReason = "", bool IsPreplanned = false, string PricingMode = "Calculated", bool PriceVisibleToCustomer = true, bool DeliveryFeeVisibleToCustomer = true, bool RequiresOutletReview = false);
public record CustomerDashboardDeliveryDto(Guid DeliveryId, Guid SubscriptionId, DateTime ScheduledDate, string MealSlot, string DeliveryWindow, string Status, string Address, double Latitude, double Longitude, int MealCount);
public record CustomerDashboardMealDto(Guid SelectionId, Guid SubscriptionId, DateTime MealDate, string MealSlot, string RecipeName, string Category, string ImageUrl, int Calories, int ProteinGrams, string Status, decimal MealPrice, Guid? DeliveryId);
public record CustomerDashboardSubscriptionDto(Guid Id, string PlanName, string DeliveryMode, int MealsPerWeek, decimal TotalCharged, DateTime NextDeliveryDate, string Status, string PaymentStatus);
public record CustomerDashboardBenefitsDto(int MealsThisWeek, int ProteinGramsThisWeek, int CaloriesThisWeek, decimal SubscriptionSavings, int DeliveryDaysThisWeek, int ActiveSubscriptions);
public record CustomerDashboardDto(IReadOnlyList<CustomerDashboardDeliveryDto> TodayDeliveries, IReadOnlyList<CustomerDashboardMealDto> TodayMeals, IReadOnlyList<CustomerDashboardSubscriptionDto> ActiveSubscriptions, CustomerDashboardBenefitsDto Benefits);
public record OrderDto(Guid Id, Guid CustomerId, Guid OutletId, decimal Total, string Status, DateTime DeliveryDate, string Address);
public record DeliveryDto(Guid Id, Guid OrderId, Guid OutletId, string CustomerName, string Address, DateTime ScheduledDate, string MealSlot, decimal DeliveryFee, string Status);
public record DriverDto(Guid Id, string Name, string Email, bool IsActive);
public record CreateDriverRequest(string FirstName, string LastName, string Email, string Password);
public record ManualDriverRouteAssignment(Guid DriverId, IReadOnlyList<Guid> AddressIds);
public record PlanDeliveryRoutesRequest(DateTime Date, IReadOnlyList<Guid> DriverIds, int MealSlot = 2, IReadOnlyList<ManualDriverRouteAssignment>? ManualAssignments = null);
public record DeliveryMapPointDto(Guid DeliveryId, Guid CustomerId, string CustomerName, Guid AddressId, string Address, double Latitude, double Longitude, int DeliveryCount, string MealSlot, string Status, Guid? RouteId, int? StopSequence);
public record DeliveryRouteStopDto(Guid Id, int StopSequence, string MealSlot, Guid AddressId, Guid CustomerId, string CustomerName, string Address, double Latitude, double Longitude, int DeliveryCount, string Status, IReadOnlyList<Guid> DeliveryIds);
public record DeliveryRouteDto(Guid Id, Guid DriverId, string DriverName, DateTime DeliveryDate, string MealSlot, string DeliveryWindow, string Status, double TotalDistanceKm, double TotalDurationMinutes, string RoutingSource, IReadOnlyList<DeliveryRouteStopDto> Stops, IReadOnlyList<IReadOnlyList<double>> Geometry);
public record DeliveryRoutePlanDto(DateTime Date, string OutletName, double OutletLatitude, double OutletLongitude, int TotalDeliveryPoints, int TotalDeliveries, int UnassignedPoints, double PlannedDistanceKm, double PlannedDurationMinutes, string OptimizationSource, IReadOnlyList<DeliveryMapPointDto> Points, IReadOnlyList<DeliveryRouteDto> Routes);
public record CreateSubscriptionRequest(Guid OutletId, string DeliveryMode, string Frequency, IReadOnlyList<MealSelectionItem> Selections, string Duration = "OneWeek", string? DiscountCode = null, IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null, string? DeliveryCity = null, Guid? LegalPolicyVersionId = null, bool LegalAccepted = false, Guid? MealPlanId = null);
public record AcceptOutletPackageRequest(Guid LegalPolicyVersionId, bool LegalAccepted);
public record UpdateMarketingPreferenceRequest(bool MarketingOptIn);
public record CustomerLegalStatusDto(Guid OutletId, string OutletName, Guid PublishedVersionId, string Version, DateTime EffectiveDateUtc, bool Accepted);
public record AcceptCustomerLegalRequest(Guid LegalPolicyVersionId, bool TermsAccepted = true, bool PrivacyAccepted = true, bool CommercialPoliciesAccepted = true);
public record CreateMealPlanRequest(string Name, string Frequency, int MealsPerDay, decimal Price, string Description, bool IsPreplanned = false, string AvailableCity = "", int? DurationDays = null);
public record CreateRecipeRequest(string Name, string Category, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, decimal PricePerMeal, decimal LargePricePerMeal, string Description = "", string ImageUrl = "", string Tags = "", IReadOnlyList<RecipeIngredientInput>? Ingredients = null, IReadOnlyList<Guid>? AllergenIds = null, int FiberGrams = 0, string MealType = "Meal");
public record UpdateRecipeRequest(string Name, string Category, int Calories, int ProteinGrams, int CarbsGrams, int FatGrams, decimal PricePerMeal, decimal LargePricePerMeal, string Description = "", string ImageUrl = "", string Tags = "", IReadOnlyList<RecipeIngredientInput>? Ingredients = null, IReadOnlyList<Guid>? AllergenIds = null, bool IsActive = true, int FiberGrams = 0, string MealType = "Meal");
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
public record RequestOutletDemoRequest(string Email, string? BusinessName = null);
public record OutletDemoRequestDto(bool Created, string Message, DateTime DemoExpiresAtUtc);
public record OutletOnboardingPaymentRequest(Guid SaaSPlanId, string BillingCycle, string BusinessType, string OutletName, string City, string State, string Pincode, string AddressLine1, string AddressLine2, string OwnerName, string Email, string OwnerPhone, string Password);
public record OutletOnboardingSessionDto(Guid Id, string AccessKey, string Status, string PaymentStatus, string PlanName, string BillingCycle, decimal SubscriptionFee, decimal SetupFee, DateTime? SubmittedAtUtc);
public record SaveOutletOnboardingDetailsRequest(string BusinessType, string OutletName, string Description, string City, string State, string Pincode, string AddressLine1, string AddressLine2, string OwnerName, string OwnerEmail, string OwnerPhone, string AadhaarNumber, string BusinessPan, string GstNumber);
public record OutletOnboardingDocumentDto(string DocumentType, string Url, string FileName, DateTime UploadedAtUtc);
public record OutletOnboardingDto(Guid Id, string Status, string PaymentStatus, string PlanName, string BillingCycle, decimal SubscriptionFee, decimal SetupFee, string BusinessType, string OutletName, string Description, string City, string State, string Pincode, string AddressLine1, string AddressLine2, string OwnerName, string OwnerEmail, string OwnerPhone, string AadhaarNumber, string BusinessPan, string GstNumber, IReadOnlyList<OutletOnboardingDocumentDto> Documents, DateTime? SubmittedAtUtc, DateTime? VerifiedAtUtc, string VerificationNotes, Guid? UserId = null, Guid? OutletId = null);
public record OutletVerificationSummaryDto(Guid Id, string OutletName, string OwnerName, string Email, string City, string BusinessType, string PlanName, string Status, string PaymentStatus, DateTime CreatedAtUtc, DateTime? SubmittedAtUtc);
public record OutletVerificationDetailDto(Guid Id, string Status, string PaymentStatus, string PlanName, string BillingCycle, decimal SubscriptionFee, decimal SetupFee, string BusinessType, string OutletName, string Description, string City, string State, string Pincode, string AddressLine1, string AddressLine2, string OwnerName, string OwnerEmail, string OwnerPhone, string AadhaarNumber, string AadhaarCardUrl, string BusinessRegistrationUrl, string BusinessPan, string BusinessPanDocumentUrl, string GstNumber, string GstCertificateUrl, DateTime? SubmittedAtUtc, string VerificationNotes);
public record DecideOutletVerificationRequest(bool Approve, string Notes = "");

public record SaveMenuItemRequest(Guid RecipeId, DayOfWeek DayOfWeek, int MealSlot, bool IsAvailable = true, int DisplayOrder = 0, string OptionGroup = "Main", bool IsRequired = true, int MaxSelections = 1);
public record BulkMenuRequest(IReadOnlyList<SaveMenuItemRequest> Items);

public record CustomerProfileDto(Guid Id, Guid CustomerId, decimal? WeightKg, decimal? HeightCm, decimal? Bmi, string Goal, string ActivityLevel, string Diet, DateTime UpdatedAtUtc, IReadOnlyList<AllergenDto> Allergies);
public record ServiceCityDto(Guid Id, string City, string State, string Country, double Latitude, double Longitude, bool IsEnabled);
public record CreateServiceCityRequest(string City, string State, string Country = "India", double Latitude = 0, double Longitude = 0, bool IsEnabled = true);
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
public record CreateCustomerAddressRequest(string City, string Pincode, string Locality, string Label, string AddressLine1, string AddressLine2, string ContactName, string ContactPhone, double Latitude, double Longitude, Guid? CityAreaId = null, bool IsDefault = false);
public record UpdateCustomerAddressRequest(string City, string Pincode, string Locality, string Label, string AddressLine1, string AddressLine2, string ContactName, string ContactPhone, double Latitude, double Longitude, Guid? CityAreaId = null, bool IsDefault = false);
public record DeliveryQuoteDto(Guid AddressId, double DistanceKm, decimal DeliveryFee, string AreaName);
public record SubscriptionQuoteRequest(Guid OutletId, string DeliveryMode, string Duration, IReadOnlyList<MealSelectionItem> Selections, string? DiscountCode = null, IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null, string? DeliveryCity = null, Guid? MealPlanId = null);
public record SubscriptionQuoteDto(decimal GrossMealAmount, decimal SubscriptionDiscountPercent, decimal SubscriptionDiscountAmount, decimal NetMealAmount, decimal RestaurantGstAmount, decimal DeliveryFee, decimal PlatformServiceFee, decimal PlatformServiceGst, decimal TotalCharged, decimal OutletCommissionPercent, decimal OutletCommissionAmount, decimal HealthAppRevenue, IReadOnlyList<DeliveryQuoteDto> DeliveryQuotes, IReadOnlyList<AllergyWarningDto> AllergyWarnings, bool RequiresAllergyConfirmation, decimal RestaurantTaxableAmount = 0m, decimal RestaurantGstRate = 0m, string RestaurantGstMode = "Exclusive", bool IsPreplanned = false, string PricingMode = "Calculated", bool PriceVisibleToCustomer = true, bool DeliveryFeeVisibleToCustomer = true, bool RequiresOutletReview = false, Guid? MealPlanId = null);
public record SubscriptionDiscountTierDto(Guid Id, Guid OutletId, int MinMeals, int? MaxMeals, decimal OneWeekPercent, decimal TwoWeeksPercent, decimal OneMonthPercent, bool IsActive);
public record SaveSubscriptionDiscountTierRequest(int MinMeals, int? MaxMeals, decimal OneWeekPercent, decimal TwoWeeksPercent, decimal OneMonthPercent, bool IsActive = true);
public record DiscountCodeDto(Guid Id, Guid? OutletId, string Code, decimal Percent, decimal? MaxAmount, int? MaxRedemptions, int RedemptionCount, DateTime? StartsAtUtc, DateTime? EndsAtUtc, bool IsActive);
public record CreateDiscountCodeRequest(string Code, decimal Percent, decimal? MaxAmount, int? MaxRedemptions, DateTime? StartsAtUtc, DateTime? EndsAtUtc, bool IsActive = true);
public record DeliveryLabelDto(Guid SubscriptionId, Guid SelectionId, DateTime MealDate, int MealSlot, string MealSlotName, string DeliveryWindow, string OutletName, string LogoUrl, string CustomerName, string CustomerPhone, string MealName, string Category, string PortionSize, string SubscriptionPlanName, string AddressLabel, string Address, string AreaName, string Pincode, decimal MealPrice, decimal DeliveryFee);
public record OutletSubscriptionMealDto(Guid SelectionId, DateTime MealDate, int MealSlot, string MealSlotName, string DeliveryWindow, Guid RecipeId, string MealName, string Category, string PortionSize, decimal MealPrice, decimal DeliveryFee, string AddressLabel, string Address, string AreaName, string Pincode, string ContactPhone, string Status);
public record OutletSubscriptionDetailDto(Guid Id, Guid CustomerId, string CustomerName, string CustomerEmail, string PlanName, string DeliveryMode, string Duration, DateTime StartDate, DateTime EndDate, string Frequency, int MealsPerDay, int MealsPerWeek, decimal MealAmount, decimal DiscountAmount, decimal RestaurantGstAmount, decimal PackageAmountWithGst, decimal DeliveryFee, string Status, IReadOnlyList<OutletSubscriptionMealDto> Meals, decimal RestaurantTaxableAmount = 0m, decimal RestaurantGstRate = 0m, string RestaurantGstMode = "Exclusive");
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
