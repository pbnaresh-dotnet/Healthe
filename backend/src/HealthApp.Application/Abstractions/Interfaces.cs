using HealthApp.Application.Strategies;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Abstractions;

public interface IIngredientRepository { Task<IReadOnlyList<Ingredient>> GetActiveAsync(); Task<IReadOnlyList<Ingredient>> GetByIdsAsync(IEnumerable<Guid> ids); }
public interface IAllergenRepository { Task<IReadOnlyList<Allergen>> GetActiveAsync(); Task<IReadOnlyList<Allergen>> GetByIdsAsync(IEnumerable<Guid> ids); }
public interface ICustomerAllergyRepository { Task<IReadOnlyList<CustomerAllergy>> GetByCustomerAsync(Guid customerId); Task ReplaceAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds); }
public interface ICatalogService { Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(); Task<IReadOnlyList<AllergenDto>> GetAllergensAsync(); }
public interface IAllergySafetyService { Task<IReadOnlyList<AllergyWarningDto>> GetWarningsAsync(Guid customerId, IReadOnlyCollection<Recipe> recipes); Task EnsureConfirmedAsync(Guid customerId, IReadOnlyCollection<Recipe> recipes, IReadOnlyCollection<Guid>? confirmedRecipeIds); }

public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public interface IUserRepository { Task<User?> FindByEmailAsync(string email); Task<User?> FindByEmailAsync(string email, Guid outletId); Task<IReadOnlyList<User>> FindTenantUsersByEmailAsync(string email); Task<User?> FindByMobileAsync(string mobileNumber, Guid? outletId = null); Task<User?> FindByIdAsync(Guid id); Task AddAsync(User user); Task UpdateAsync(User user); Task<IReadOnlyList<User>> GetAllAsync(); Task<int> CountAsync(UserRole? role = null); Task<PageResult<User>> GetOutletCustomersPageAsync(Guid outletId, string? search, int page, int pageSize); }
public interface IOutletBrandingRepository
{
    Task<OutletBranding?> GetByOutletAsync(Guid outletId);
    Task AddAsync(OutletBranding branding);
    Task UpdateAsync(OutletBranding branding);
}
public interface IOutletDomainRepository
{
    Task<OutletDomain?> GetAsync(Guid id);
    Task<OutletDomain?> GetActiveByHostnameAsync(string hostname);
    Task<OutletDomain?> GetByHostnameAsync(string hostname);
    Task<IReadOnlyList<OutletDomain>> GetByOutletAsync(Guid outletId);
    Task<IReadOnlyList<OutletDomain>> GetAllAsync();
    Task AddAsync(OutletDomain domain);
    Task UpdateAsync(OutletDomain domain);
}


public interface IOutletGroupRepository
{
    Task<IReadOnlyList<OutletGroup>> GetAllAsync();
    Task<OutletGroup?> GetAsync(Guid id);
    Task AddAsync(OutletGroup group);
    Task UpdateAsync(OutletGroup group);
    Task<bool> HasOutletsAsync(Guid groupId);
}

public interface IOutletTaxProfileRepository
{
    Task<OutletTaxProfile?> GetCurrentAsync(Guid outletId, DateTime? asOfUtc = null);
    Task<OutletTaxProfile?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<OutletTaxProfile>> GetHistoryAsync(Guid outletId);
    Task AddVersionAsync(OutletTaxProfile profile, DateTime effectiveFromUtc);
}

public interface IPlatformTaxProfileRepository
{
    Task<PlatformTaxProfile?> GetCurrentAsync(DateTime? asOfUtc = null);
    Task<PlatformTaxProfile?> GetByIdAsync(Guid id);
}

public interface IFinancePolicyRepository
{
    Task<FinancePolicyDocument?> GetDocumentAsync(string code);
    Task<IReadOnlyList<FinancePolicyDocumentVersion>> GetVersionsAsync(Guid documentId);
    Task<IReadOnlyList<FinancePolicyDocumentSection>> GetSectionsAsync(Guid versionId);
}

public interface IFinancePolicyService
{
    Task<FinancePolicyDocumentDto?> GetAsync(string code = "FINANCE-CALCULATION-POLICY", DateTime? asOfUtc = null);
    Task<IReadOnlyList<FinancePolicyVersionDto>> GetHistoryAsync(string code = "FINANCE-CALCULATION-POLICY");
}

public interface IFinanceTaxRuleRepository
{
    Task<FinanceTaxRule?> GetEffectiveAsync(FinanceSupplyType supplyType, TaxOperatingMode? taxOperatingMode, DateTime asOfUtc);
    Task<FinanceTaxRule?> GetByIdAsync(Guid id);
}

public interface IFinanceTaxConfigurationService
{
    Task<FinanceTaxCalculationConfiguration> ResolveAsync(Guid outletId, DateTime asOfUtc);
}

public interface IFinanceCalculationSnapshotRepository
{
    Task<FinanceCalculationSnapshot?> GetBySourceAsync(string sourceType, Guid sourceId);
    Task AddAsync(FinanceCalculationSnapshot snapshot);
}

public interface IFinanceCalculationSnapshotService
{
    Task<FinanceCalculationSnapshot> CreateAsync(
        Guid outletId,
        string sourceType,
        Guid sourceId,
        DateTime calculatedAtUtc,
        FinanceTaxCalculationConfiguration configuration,
        TaxBreakdown calculation,
        decimal restaurantBaseAmount,
        decimal platformServiceFee,
        decimal platformServiceFeePercent,
        bool gatewayCostsIncludedInPlatformFee,
        decimal commissionRatePercent,
        decimal commissionAmount,
        Guid? discountTierId = null,
        decimal discountPercent = 0m,
        decimal discountAmount = 0m,
        string discountRuleSnapshotJson = "",
        Guid? discountCodeId = null,
        decimal discountCodeAmount = 0m,
        decimal totalDiscountAmount = 0m);
}

public interface IFinancialDocumentRepository
{
    Task<FinancialDocument?> GetBySourceAsync(string sourceType, Guid sourceId);
    Task<IReadOnlyList<FinancialDocument>> GetBySourcePrefixAsync(string sourceTypePrefix, Guid sourceId);
    Task AddAsync(FinancialDocument document);
    Task AddLineAsync(FinancialDocumentLine line);
    Task AddTaxComponentAsync(FinancialTaxComponent component);
}

public interface IFinancialDocumentService
{
    Task<IReadOnlyList<FinancialDocument>> CreateDraftsForSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}

public interface IOutletRepository { Task<IReadOnlyList<Outlet>> GetAllAsync(); Task<int> CountAsync(OutletStatus? status = null); Task<PageResult<Outlet>> GetPageAsync(string? search, string? status, string? city, int page, int pageSize); Task<Outlet?> GetByIdAsync(Guid id); Task<Outlet?> GetBySlugAsync(string slug); Task<Outlet?> GetBySubdomainAsync(string subdomain); Task AddAsync(Outlet outlet); Task UpdateAsync(Outlet outlet); }
public interface IOutletLegalPolicyRepository
{
    Task<OutletLegalPolicyVersion?> GetPublishedAsync(Guid outletId);
    Task<OutletLegalPolicyVersion?> GetByIdAsync(Guid id, Guid outletId);
    Task<OutletLegalPolicyVersion?> GetByVersionAsync(Guid outletId, string version);
    Task<IReadOnlyList<OutletLegalPolicyVersion>> GetHistoryAsync(Guid outletId);
    Task PublishVersionAsync(OutletLegalPolicyVersion version);
    Task<bool> HasAcceptedVersionAsync(Guid customerId, Guid outletId, Guid versionId);
    Task AddAcceptanceAsync(CustomerLegalAcceptance acceptance);
}
public interface ISaaSPlanRepository { Task<IReadOnlyList<SaaSPlan>> GetActiveAsync(); Task<SaaSPlan?> GetAsync(Guid id); }
public interface IOutletSubscriptionRepository { Task<OutletSubscription?> GetByOutletAsync(Guid outletId); Task<OutletSubscription?> GetAnyByOutletAsync(Guid outletId); Task<IReadOnlySet<Guid>> GetActiveOutletIdsAsync(); Task AddAsync(OutletSubscription subscription); Task UpdateAsync(OutletSubscription subscription); }
public interface IPlatformTransactionRepository { Task AddAsync(PlatformTransaction transaction); Task UpdateAsync(PlatformTransaction transaction); Task<IReadOnlyList<PlatformTransaction>> GetAllAsync(); Task<bool> ExistsByReferenceAsync(string referenceId); }
public interface IOutletOnboardingRepository
{
    Task<OutletOnboardingApplication?> GetAsync(Guid id);
    Task<OutletOnboardingApplication?> GetByUserIdAsync(Guid userId);
    Task<IReadOnlyList<OutletOnboardingApplication>> GetByStatusAsync(string status);
    Task AddAsync(OutletOnboardingApplication application);
    Task UpdateAsync(OutletOnboardingApplication application);
}

public interface IOutletOnboardingService
{
    Task<IReadOnlyList<SaaSPlanDto>> GetPlansAsync();
    Task<OutletOnboardingSessionDto> StartPaymentAsync(OutletOnboardingPaymentRequest request);
    Task<OutletOnboardingDto?> GetCurrentAsync();
    Task<OutletOnboardingDto?> SaveCurrentDetailsAsync(SaveOutletOnboardingDetailsRequest request);
    Task<OutletOnboardingDocumentDto?> UploadCurrentDocumentAsync(string documentType, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<OutletOnboardingDto?> SubmitCurrentAsync();
    Task<ProtectedFileDownload?> GetCurrentDocumentAsync(string documentType);
    Task<OutletOnboardingDto?> GetAsync(Guid id, string accessKey);
    Task<ProtectedFileDownload?> GetDocumentAsync(Guid id, string accessKey, string documentType);
    Task<OutletOnboardingDto?> SaveDetailsAsync(Guid id, string accessKey, SaveOutletOnboardingDetailsRequest request);
    Task<OutletOnboardingDocumentDto?> UploadDocumentAsync(Guid id, string accessKey, string documentType, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<OutletOnboardingDto?> SubmitAsync(Guid id, string accessKey);
}

public interface IOutletVerificationService
{
    Task<IReadOnlyList<OutletVerificationSummaryDto>> GetPendingAsync();
    Task<OutletVerificationDetailDto?> GetAsync(Guid id);
    Task<ProtectedFileDownload?> GetDocumentAsync(Guid id, string documentType);
    Task<OutletVerificationDetailDto?> DecideAsync(Guid id, DecideOutletVerificationRequest request);
}

public interface IMealPlanRepository { Task<IReadOnlyList<MealPlan>> GetByOutletAsync(Guid outletId); Task<MealPlan?> GetAsync(Guid id); Task AddAsync(MealPlan plan); }
public interface IRecipeRepository { Task<IReadOnlyList<Recipe>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<Recipe>> GetByOutletAndCategoryAsync(Guid outletId, string? category); Task<PageResult<Recipe>> GetByOutletPageAsync(Guid outletId, string? category, string? search, int page, int pageSize); Task<IReadOnlyList<Recipe>> GetByIdsAsync(IEnumerable<Guid> ids); Task<IReadOnlyList<Recipe>> GetByIdsForOutletAsync(IEnumerable<Guid> ids, Guid outletId); Task<Recipe?> GetAsync(Guid id); Task<Recipe?> GetForOutletAsync(Guid id, Guid outletId); Task AddAsync(Recipe recipe); Task UpdateAsync(Recipe recipe); Task DeleteAsync(Guid id); Task<bool> DeleteAsync(Guid id, Guid outletId); }
public interface IOutletMenuRepository { Task<IReadOnlyList<OutletMenuItem>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<OutletMenuItem>> GetByOutletDayAsync(Guid outletId, DayOfWeek day); Task AddAsync(OutletMenuItem item); Task DeleteAsync(Guid id); Task ReplaceAsync(Guid outletId, IEnumerable<OutletMenuItem> items); }
public interface ISubscriptionRepository { Task<IReadOnlyList<Subscription>> GetByCustomerAsync(Guid customerId); Task<IReadOnlyList<Subscription>> GetByOutletAsync(Guid outletId); Task<PageResult<Subscription>> GetByOutletPageAsync(Guid outletId, string? search, string? status, int page, int pageSize); Task<Subscription?> GetAsync(Guid id); Task AddAsync(Subscription subscription); Task UpdateAsync(Subscription subscription); }
public interface ISubscriptionMealSelectionRepository { Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAsync(Guid subscriptionId); Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAndDateRangeAsync(Guid subscriptionId, DateTime from, DateTime to); Task<IReadOnlyList<SubscriptionMealSelection>> GetByOutletAndDateRangeAsync(Guid outletId, DateTime from, DateTime to); Task<SubscriptionMealSelection?> GetAsync(Guid id); Task AddRangeAsync(IEnumerable<SubscriptionMealSelection> selections); Task UpdateAsync(SubscriptionMealSelection selection); Task DeleteBySubscriptionAndDateRangeAsync(Guid subscriptionId, DateTime from, DateTime to); }
public interface ICustomerCreditRepository { Task<decimal> GetBalanceAsync(Guid customerId); Task<decimal> GetOutstandingLateSkipAmountAsync(Guid customerId); Task<IReadOnlyList<CustomerCreditTransaction>> GetTransactionsAsync(Guid customerId); Task AddAsync(CustomerCreditTransaction transaction); }
public interface IOrderRepository { Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId); Task<IReadOnlyList<Order>> GetByOutletAsync(Guid outletId); Task<PageResult<Order>> GetByOutletPageAsync(Guid outletId, string? search, string? status, int page, int pageSize); Task<Order?> GetBySubscriptionAsync(Guid subscriptionId); Task AddAsync(Order order); Task UpdateAsync(Order order); }
public interface IDeliveryRepository { Task<IReadOnlyList<Delivery>> GetByOutletAsync(Guid outletId); Task<PageResult<Delivery>> GetByOutletPageAsync(Guid outletId, string? search, string? status, DateTime? date, int page, int pageSize); Task<IReadOnlyList<Delivery>> GetByOutletAndDateRangeAsync(Guid outletId, DateTime from, DateTime to); Task AddAsync(Delivery delivery); Task<Delivery?> GetAsync(Guid id); Task UpdateAsync(Delivery delivery); Task<IReadOnlyList<Delivery>> GetBySubscriptionAsync(Guid subscriptionId); }
public interface ITokenService { AuthResponse CreateToken(User user); }
public sealed record FileStorageResult(string Url, string Key, string ContentType);
public sealed record FileStorageDownload(Stream Content, string ContentType);
public sealed record ProtectedFileDownload(Stream Content, string ContentType, string FileName);
public interface IFileStorage { Task<FileStorageResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default); Task<FileStorageResult> UploadPrivateAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default); Task<FileStorageDownload?> OpenReadAsync(string key, CancellationToken cancellationToken = default); }
public interface IGeocodingService { Task<ReverseGeocodeDto?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken = default); }
public interface IPasswordService { string Hash(string password); bool Verify(string password, string hash); }
public interface ICurrentUser { Guid? UserId { get; } Guid? OutletId { get; } string? Role { get; } bool IsAuthenticated { get; } }
public interface ITenantContext { Guid? OutletId { get; } string? OutletSlug { get; } bool IsResolved { get; } void Set(Guid outletId, string outletSlug); }
public interface ITenantHostResolver { Task<Outlet?> ResolveAsync(string? hostname); }
public sealed class TenantDomainSettings { public string PlatformBaseDomain { get; set; } = "healthapp.com"; }
public sealed class CloudflarePagesSettings
{
    public bool Enabled { get; set; }
    public string AccountId { get; set; } = "";
    public string ProjectName { get; set; } = "healthapp-customer";
    public string ApiToken { get; set; } = "";
}
public sealed record CloudflarePagesDomainState(
    string Name,
    string Status,
    string ValidationMethod,
    string ValidationStatus,
    string? ValidationError,
    string? TxtName,
    string? TxtValue,
    string VerificationStatus,
    string? VerificationError);
public interface ICloudflarePagesService
{
    bool IsEnabled { get; }
    Task<CloudflarePagesDomainState> EnsureDomainAsync(string hostname, CancellationToken cancellationToken = default);
    Task<CloudflarePagesDomainState?> GetDomainAsync(string hostname, CancellationToken cancellationToken = default);
    Task<CloudflarePagesDomainState?> RetryValidationAsync(string hostname, CancellationToken cancellationToken = default);
}
public sealed record LegalAcceptanceContext(string? IpAddress, string? UserAgent);
public interface IAuthService { Task<AuthResponse?> LoginAsync(LoginRequest request); Task<AuthResponse> RegisterAsync(RegisterRequest request, LegalAcceptanceContext? acceptanceContext = null); }
public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody, string? ReplyTo = null);
public enum EmailTemplateId
{
    SmtpTest,
    OutletDemoAccess,
    CustomerWelcome,
    OutletOnboardingPaymentConfirmed,
    OutletVerificationSubmitted,
    OutletVerificationApproved,
    OutletVerificationRejected,
    PackageCreated,
    PackageAccepted,
    PackagePaymentConfirmed,
    SubscriptionCreated,
    MealSkipped,
    MealRescheduled,
    PasswordReset,
    EmailVerification,
    DeliveryReminder,
    PaymentFailed,
    RefundProcessed,
    SaaSRenewalReminder
}
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
public interface ITransactionalEmailService
{
    Task SendAsync(EmailTemplateId template, string to, IReadOnlyDictionary<string, string?> data, CancellationToken cancellationToken = default);
    Task<bool> TrySendAsync(EmailTemplateId template, string to, IReadOnlyDictionary<string, string?> data, CancellationToken cancellationToken = default);
}
public interface IOutletUrlService
{
    Task<string> GetStorefrontUrlAsync(Guid outletId, CancellationToken cancellationToken = default);
}
public interface IOutletDemoService { Task<OutletDemoRequestDto> RequestAsync(RequestOutletDemoRequest request, CancellationToken cancellationToken = default); }
public interface ITrialRepository { Task<Trial?> GetByOutletAsync(Guid outletId); Task AddAsync(Trial trial); Task UpdateAsync(Trial trial); }
public interface ITrialService { Task<TrialDto?> GetCurrentAsync(); Task<TrialDto?> StartAsync(StartTrialRequest request); Task<TrialDto?> ConvertAsync(ChangeOutletSubscriptionRequest request); Task<TrialDto?> CancelAsync(string reason = "Cancelled by outlet administrator"); }
public interface IMarketplaceService { Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync(); Task<AvailabilityResponse> GetAvailabilityAsync(double latitude, double longitude, string? city = null); Task<IReadOnlyList<CityDto>> GetCitiesAsync(); Task<IReadOnlyList<OutletDto>> GetAllOutletsAsync(string? city = null); Task<OutletDto?> GetOutletAsync(string slug); Task<OutletLegalPoliciesDto?> GetOutletLegalAsync(string slug); Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(Guid outletId, string? city = null); Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(Guid outletId, string? category); Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(Guid outletId); }
public interface ICustomerService { Task<UserDto?> GetProfileAsync(); Task<UserDto?> UpdateMarketingPreferenceAsync(UpdateMarketingPreferenceRequest request); Task<CustomerLegalStatusDto?> GetLegalStatusAsync(); Task<CustomerLegalStatusDto?> AcceptLegalAsync(AcceptCustomerLegalRequest request, LegalAcceptanceContext? acceptanceContext = null); Task<CustomerDashboardDto?> GetDashboardAsync(); Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync(); Task<IReadOnlyList<OrderDto>> GetOrdersAsync(); Task<SubscriptionDto?> SubscribeAsync(CreateSubscriptionRequest request, LegalAcceptanceContext? acceptanceContext = null); Task<SubscriptionQuoteDto?> QuoteAsync(SubscriptionQuoteRequest request); Task<IReadOnlyList<RecipeDto>> GetSubscriptionRecipesAsync(Guid subscriptionId, string? category); Task<IReadOnlyList<MenuItemDto>> GetSubscriptionMenuAsync(Guid subscriptionId); Task<IReadOnlyList<MealSelectionDto>> GetMealSelectionsAsync(Guid subscriptionId, DateTime? weekStart); Task<IReadOnlyList<MealSelectionDto>> SaveMealSelectionsAsync(Guid subscriptionId, SaveMealSelectionsRequest request); Task<MealSelectionDto?> SkipMealAsync(Guid subscriptionId, Guid selectionId, SkipMealRequest request); Task<IReadOnlyList<MealSelectionDto>> SkipDayAsync(Guid subscriptionId, DateTime date, SkipDayRequest request); Task<MealSelectionDto?> RescheduleMealAsync(Guid subscriptionId, Guid selectionId, RescheduleMealRequest request); Task<CreditBalanceDto> GetCreditBalanceAsync(); Task<IReadOnlyList<CreditTransactionDto>> GetCreditTransactionsAsync(); }
public interface IOutletSettingsService
{
    Task<OutletSettingsDto?> GetAsync();
    Task<IReadOnlyList<OutletDomainDto>> GetDomainsAsync();
    Task<OutletDomainDto> RequestDomainAsync(RequestOutletDomainRequest request);
    Task<OutletDomainDto> VerifyDomainAsync(Guid domainId, bool activateIfReady = true);
    Task<OutletSettingsDto?> UpdateDeliveryDaysAsync(UpdateOutletSettingsRequest request);
    Task<OutletSettingsDto?> UpdatePackageSettingsAsync(UpdateOutletPackageSettingsRequest request);
    Task<OutletSettingsDto?> UpdateLateSkipFeeAsync(UpdateOutletLateSkipFeeRequest request);
    Task<OutletLegalPoliciesDto?> GetLegalPoliciesAsync();
    Task<OutletLegalPoliciesDto?> UpdateLegalPoliciesAsync(UpdateOutletLegalPoliciesRequest request);
    Task<OutletBrandingDto?> UpdateBrandingAsync(UpdateOutletBrandingRequest request);
    Task<OutletBrandingDto?> UpdateBrandingAssetAsync(string assetType, string url);

    Task<OutletReadinessDto?> GetReadinessAsync();
    Task<OutletReadinessDto?> GoLiveAsync();
}

public record CreateOutletStaffRequest(string FirstName, string LastName, string Email, string Password, string Role);
public record UpdateOutletStaffRequest(string FirstName, string LastName, string Email, string Role, bool IsActive, string? Password = null);
public record OutletStaffDto(Guid Id, string Name, string Email, string Role, bool IsActive);

public interface IOutletStaffService
{
    Task<IReadOnlyList<OutletStaffDto>> GetAsync();
    Task<OutletStaffDto?> CreateAsync(CreateOutletStaffRequest request);
    Task<OutletStaffDto?> UpdateAsync(Guid id, UpdateOutletStaffRequest request);
}

public interface IIngredientConsumptionService
{
    Task<DailyIngredientConsumptionReportDto> GetDailyAsync(DateTime date);
}

public interface IOutletService { Task<OutletTaxSettingsDto?> GetTaxSettingsAsync(); Task<OutletTaxSettingsDto?> UpdateTaxSettingsAsync(UpdateOutletTaxSettingsRequest request); Task<OutletDashboardDto> GetDashboardAsync(); Task<OutletSubscriptionDetailDto?> GetSubscriptionDetailAsync(Guid subscriptionId); Task<OutletKitchenDayDto> GetKitchenDayAsync(DateTime date); Task<OutletBillingDto?> GetBillingAsync(); Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync(); Task<OutletBillingDto?> ChangeSubscriptionAsync(ChangeOutletSubscriptionRequest request); Task<OutletDto?> GetCurrentAsync(); Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(); Task<MealPlanDto?> CreatePlanAsync(CreateMealPlanRequest request); Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(string? category); Task<PageResult<RecipeDto>> GetRecipesPageAsync(string? category, string? search, int page, int pageSize); Task<RecipeDto?> CreateRecipeAsync(CreateRecipeRequest request); Task<RecipeDto?> UpdateRecipeAsync(Guid recipeId, UpdateRecipeRequest request); Task<bool> DeleteRecipeAsync(Guid recipeId); Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(); Task<IReadOnlyList<MenuItemDto>> SaveMenuAsync(BulkMenuRequest request); Task<IReadOnlyList<UserDto>> GetCustomersAsync(); Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync(); Task<PageResult<SubscriptionDto>> GetSubscriptionsPageAsync(string? search, string? status, int page, int pageSize); Task<IReadOnlyList<OrderDto>> GetOrdersAsync(); Task<PageResult<OrderDto>> GetOrdersPageAsync(string? search, string? status, int page, int pageSize); Task<IReadOnlyList<DeliveryDto>> GetDeliveriesAsync(); Task<PageResult<DeliveryDto>> GetDeliveriesPageAsync(string? search, string? status, DateTime? date, int page, int pageSize); }
public interface IAdminOutletLifecycleService
{
    Task<AdminOutletReactivationOptionsDto?> GetReactivationOptionsAsync(Guid outletId);
    Task<AdminOutletReactivationDto?> ReactivateAsync(Guid outletId, AdminOutletReactivationRequest request);
}

public interface IAdminService
{
    Task<IReadOnlyList<OutletGroupDto>> GetOutletGroupsAsync();
    Task<OutletGroupDto> CreateOutletGroupAsync(CreateOutletGroupRequest request);
    Task<OutletGroupDto> UpdateOutletGroupAsync(Guid id, UpdateOutletGroupRequest request);
    Task<OutletGroupDto?> AssignOutletGroupAsync(Guid outletId, Guid? groupId);
    Task<AdminOutlet360Dto?> GetOutlet360Async(Guid outletId);
    Task<AdminFinanceReportDto> GetFinanceReportAsync(AdminFinanceReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutletDto>> GetOutletsAsync();
    Task<PageResult<OutletDto>> GetOutletsPageAsync(string? search, string? status, string? city, int page, int pageSize);
    Task<IReadOnlyList<UserDto>> GetUsersAsync();
    Task<object> GetDashboardAsync();
    Task<PlatformRevenueDto> GetRevenueAsync();
    Task<IReadOnlyList<OutletDomainDto>> GetOutletDomainsAsync();
    Task<OutletDomainDto> SetOutletDomainStatusAsync(Guid domainId, OutletDomainStatus status);
    Task<ApplicationErrorPageDto> GetErrorsAsync(ApplicationErrorQueryRequest request);
    Task<ApplicationErrorDetailDto?> GetErrorAsync(Guid id);
    Task<ApplicationErrorDetailDto?> ResolveErrorAsync(Guid id, string resolutionNotes);
}


public interface ICustomerProfileRepository { Task<CustomerProfile?> GetAsync(Guid customerId); Task AddOrUpdateAsync(CustomerProfile profile); }
public interface IServiceCityRepository { Task<IReadOnlyList<ServiceCity>> GetAllAsync(); Task<IReadOnlyList<ServiceCity>> GetEnabledAsync(); Task<ServiceCity?> GetByCityAsync(string city); Task<ServiceCity?> GetByIdAsync(Guid id); Task AddAsync(ServiceCity city); Task UpdateAsync(ServiceCity city); }
public interface ICityAreaRepository { Task<IReadOnlyList<CityArea>> GetActiveAsync(string? city = null); Task<CityArea?> GetAsync(Guid id); Task AddAsync(CityArea area); }
public interface IOutletDeliveryAreaRepository { Task<IReadOnlyList<OutletDeliveryArea>> GetByOutletAsync(Guid outletId); Task<IReadOnlyList<CityArea>> GetAreasForOutletAsync(Guid outletId); Task ReplaceAsync(Guid outletId, IEnumerable<OutletDeliveryArea> areas); }
public interface IDeliveryPricingRepository { Task<IReadOnlyList<DeliveryPricingRule>> GetByOutletAsync(Guid outletId); Task AddAsync(DeliveryPricingRule rule); Task DeleteAsync(Guid id, Guid outletId); }
public interface ICustomerAddressRepository { Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId); Task<CustomerAddress?> GetAsync(Guid customerId, Guid id); Task AddAsync(CustomerAddress address); Task UpdateAsync(CustomerAddress address); Task DeleteAsync(Guid customerId, Guid id); }
public interface ICustomerLikedMealRepository { Task<IReadOnlyList<CustomerLikedMeal>> GetByCustomerAsync(Guid customerId); Task<bool> ExistsAsync(Guid customerId, Guid recipeId); Task AddAsync(CustomerLikedMeal meal); Task RemoveAsync(Guid customerId, Guid recipeId); }
public interface ISubscriptionDiscountTierRepository { Task<IReadOnlyList<SubscriptionDiscountTier>> GetByOutletAsync(Guid outletId); Task AddAsync(SubscriptionDiscountTier tier); Task UpdateAsync(SubscriptionDiscountTier tier); Task DeleteAsync(Guid outletId, Guid id); }
public interface IMealSelectionHistoryRepository { Task AddAsync(MealSelectionHistory history); Task<IReadOnlyList<MealSelectionHistory>> GetBySelectionAsync(Guid selectionId); }
public sealed record PaymentGatewaySettlementInput(
    Guid PaymentTransactionId,
    string ProviderSettlementId,
    decimal GatewayFeeAmount,
    decimal GatewayFeeTaxAmount,
    decimal OtherProviderAdjustmentAmount,
    DateTime? SettledAtUtc,
    string SourceDataJson,
    string ReconciliationReference,
    string ReconciledBy,
    decimal? ReportedGrossAmount = null,
    decimal? ReportedNetSettlementAmount = null);

public interface IPaymentSettlementAccountingService
{
    Task<PaymentGatewaySettlement> RecordSettlementAsync(PaymentGatewaySettlementInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentGatewaySettlement>> GetUnreconciledAsync(Guid? outletId = null, CancellationToken cancellationToken = default);
    Task<PaymentSettlementImportResultDto> ImportCsvAsync(string provider, Stream csv, string reconciledBy, CancellationToken cancellationToken = default);
}

public interface IPaymentGatewaySettlementRepository
{
    Task<PaymentGatewaySettlement?> GetByPaymentTransactionAsync(Guid paymentTransactionId);
    Task<PaymentGatewaySettlement?> GetByProviderPaymentIdAsync(string provider, string providerPaymentId);
    Task AddAsync(PaymentGatewaySettlement settlement);
    Task UpdateAsync(PaymentGatewaySettlement settlement);
    Task<IReadOnlyList<PaymentGatewaySettlement>> GetUnreconciledAsync(Guid? outletId = null);
}

public interface IPaymentSettlementReconciliationExceptionRepository
{
    Task<PaymentSettlementReconciliationException?> GetOpenAsync(string provider, string providerPaymentId, string providerSettlementId, string exceptionType);
    Task AddAsync(PaymentSettlementReconciliationException exception);
    Task UpdateAsync(PaymentSettlementReconciliationException exception);
    Task<IReadOnlyList<PaymentSettlementReconciliationException>> GetOpenAsync(string? provider = null, Guid? outletId = null);
}
public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetAsync(Guid id);
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string provider, string key);
    Task<PaymentTransaction?> GetByProviderOrderIdAsync(string providerOrderId);
    Task<PaymentTransaction?> GetByProviderPaymentIdAsync(string provider, string providerPaymentId);
    Task<PaymentTransaction?> GetByOnboardingApplicationIdAsync(Guid applicationId);
    Task<PaymentTransaction?> GetLatestBySubscriptionAsync(Guid subscriptionId);
    Task<IReadOnlyList<PaymentTransaction>> GetRetryableAsync(DateTime utcNow, DateTime staleClaimBeforeUtc, int maxAttempts, int batchSize);
    Task<bool> TryClaimRetryAsync(Guid paymentId, DateTime utcNow, int maxAttempts);
    Task AddAsync(PaymentTransaction payment);
    Task UpdateAsync(PaymentTransaction payment);
}
public sealed record PaymentGatewayCreateOrderRequest(
    string OrderId,
    decimal Amount,
    string Currency,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ReturnUrl,
    string NotifyUrl,
    string OrderNote,
    string IdempotencyKey);
public sealed record PaymentGatewayCheckoutSession(string ProviderOrderId, string PaymentSessionId, string Status);
public sealed record PaymentGatewayTransactionStatus(
    string ProviderPaymentId,
    string PaymentStatus,
    string? PaymentMessage,
    string? PaymentMethod,
    decimal? Amount,
    string Currency);
public interface IPaymentGatewayAdapter : IPaymentGateway { }
public interface IPaymentGatewayFactory
{
    IPaymentGateway Get(string provider);
}
public sealed record PaymentGatewayWebhookEvent(string ProviderOrderId, string EventType);

public interface IPaymentGateway
{
    string Provider { get; }
    string OutletReturnUrl { get; }
    string WebhookUrl { get; }
    Task<PaymentGatewayCheckoutSession> CreateOrderAsync(PaymentGatewayCreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentGatewayTransactionStatus>> GetPaymentsAsync(string providerOrderId, CancellationToken cancellationToken = default);
    bool VerifyWebhookSignature(IReadOnlyDictionary<string, string> headers, string rawBody);
    PaymentGatewayWebhookEvent? ParseWebhook(string rawBody);
}
public interface IDiscountCodeRepository { Task<DiscountCode?> GetAsync(Guid? outletId, string code); Task<IReadOnlyList<DiscountCode>> GetByOutletAsync(Guid outletId); Task AddAsync(DiscountCode code); Task UpdateAsync(DiscountCode code); }
public interface IOrderFinancialRepository { Task AddAsync(OrderFinancialBreakdown breakdown); Task<OrderFinancialBreakdown?> GetByOrderAsync(Guid orderId); }
public interface IDeliveryCalculator { Task<DeliveryQuoteDto> QuoteAsync(Guid outletId, Guid customerId, Guid addressId); Task<decimal> CalculateForSelectionsAsync(Guid outletId, Guid customerId, SubscriptionDeliveryMode mode, IReadOnlyList<SubscriptionMealSelection> selections); }

public interface ICustomerAddressService { Task<IReadOnlyList<CustomerAddressDto>> GetAsync(); Task<CustomerAddressDto?> CreateAsync(CreateCustomerAddressRequest request); Task<CustomerAddressDto?> UpdateAsync(Guid id, UpdateCustomerAddressRequest request); Task<bool> DeleteAsync(Guid id); Task<IReadOnlyList<DeliveryQuoteDto>> QuoteAsync(Guid outletId); }
public interface ICustomerProfileService { Task<CustomerProfileDto?> GetAsync(); Task<CustomerProfileDto?> SaveAsync(SaveCustomerProfileRequest request); }
public interface IOutletDeliveryService { Task<IReadOnlyList<CityAreaDto>> GetAvailableAreasAsync(string? city); Task<IReadOnlyList<OutletDeliveryAreaDto>> GetAreasAsync(); Task<IReadOnlyList<DeliveryPricingRuleDto>> GetPricingAsync(); Task<IReadOnlyList<OutletDeliveryAreaDto>> SaveAreasAsync(SaveOutletDeliveryAreasRequest request); Task<DeliveryPricingRuleDto?> AddPricingAsync(CreateDeliveryPricingRuleRequest request); Task<bool> DeletePricingAsync(Guid id); }
public interface IDiscountConfigurationService { Task<IReadOnlyList<SubscriptionDiscountTierDto>> GetTiersAsync(); Task<SubscriptionDiscountTierDto?> AddTierAsync(SaveSubscriptionDiscountTierRequest request); Task<SubscriptionDiscountTierDto?> UpdateTierAsync(Guid id, SaveSubscriptionDiscountTierRequest request); Task<bool> DeleteTierAsync(Guid id); }
public interface IServiceCityAdminService { Task<IReadOnlyList<ServiceCityDto>> GetAsync(); Task<ServiceCityDto?> CreateAsync(CreateServiceCityRequest request); Task<ServiceCityDto?> SetEnabledAsync(Guid id, bool enabled); }
public interface ICityAreaAdminService { Task<IReadOnlyList<CityAreaDto>> GetAsync(string? city); Task<CityAreaDto?> CreateAsync(CreateCityAreaRequest request); }
public interface IPaymentService
{
    Task<PaymentCheckoutDto?> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentDto?> GetAsync(Guid id);
    Task<PaymentWebhookResultDto> HandleWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
    Task RetryPendingAsync(Guid paymentId, CancellationToken cancellationToken = default);
}

public interface IDeliveryLabelService { Task<IReadOnlyList<DeliveryLabelDto>> GetLabelsAsync(DateTime? date); }
public interface IDeliveryRouteRepository
{
    Task<IReadOnlyList<DeliveryRoute>> GetByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot);
    Task<IReadOnlyList<DeliveryRoute>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<DeliveryRoute?> GetAsync(Guid id);
    Task UpdateAsync(DeliveryRoute route);
    Task DeleteByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot);
    Task AddAsync(DeliveryRoute route);
}
public record RouteOptimizationStop(Guid Id, double Latitude, double Longitude);
public record RouteOptimizationResult(double DistanceKm, double DurationMinutes, IReadOnlyList<Guid> OrderedStopIds, IReadOnlyList<IReadOnlyList<double>> Geometry);
public record RouteTravelMatrix(long[,] DurationSeconds, double[,] DistanceMeters);
public record DriverRouteAssignment(Guid DriverId, IReadOnlyList<Guid> StopIds);
public record MultiDriverRoutePlan(IReadOnlyList<DriverRouteAssignment> Routes);

public interface IRouteOptimizationService
{
    Task<RouteOptimizationResult> RouteInOrderAsync(double outletLatitude, double outletLongitude, IReadOnlyList<RouteOptimizationStop> stops, CancellationToken cancellationToken = default);
}
public interface IRouteMatrixService
{
    Task<RouteTravelMatrix> BuildAsync(IReadOnlyList<RouteOptimizationStop> points, CancellationToken cancellationToken = default);
}
public interface IMultiDriverRoutePlanningService
{
    Task<MultiDriverRoutePlan> OptimizeAsync(IReadOnlyList<Guid> driverIds, IReadOnlyList<RouteOptimizationStop> points, RouteTravelMatrix matrix, CancellationToken cancellationToken = default);
}
public interface IDeliveryRouteService
{
    Task<IReadOnlyList<DriverDto>> GetDriversAsync();
    Task<DriverDto?> CreateDriverAsync(CreateDriverRequest request);
    Task<DeliveryRoutePlanDto> GetPlanAsync(DateTime date, int mealSlot = 2);
    Task<DeliveryRoutePlanDto> PlanRoutesAsync(PlanDeliveryRoutesRequest request);
    Task<DeliveryRoutePlanDto> DispatchRouteAsync(Guid routeId);
    Task<DeliveryRoutePlanDto?> GetDriverPlanAsync(DateTime date, int mealSlot = 2);
    Task<DeliveryRoutePlanDto?> StartDriverRouteAsync(Guid routeId);
    Task<DeliveryRoutePlanDto?> PickUpDriverStopAsync(Guid stopId); Task<DeliveryRoutePlanDto?> PickUpDriverDeliveryAsync(Guid deliveryId);
    Task<DeliveryRoutePlanDto?> CompleteDriverStopAsync(Guid stopId);
}
public interface IOutletDiscountCodeService { Task<IReadOnlyList<DiscountCodeDto>> GetAsync(); Task<DiscountCodeDto?> CreateAsync(CreateDiscountCodeRequest request); Task<bool> DisableAsync(Guid id); }

public interface IOutletPackageService
{
    Task<IReadOnlyList<UserDto>> GetCustomersAsync();
    Task<PageResult<UserDto>> GetCustomersPageAsync(string? search, int page, int pageSize);
    Task<UserDto?> CreateCustomerAsync(CreateOutletCustomerRequest request);
    Task<OutletCustomerProfileDto?> GetCustomerProfileAsync(Guid customerId);
    Task<OutletCustomerProfileDto?> UpdateCustomerProfileAsync(Guid customerId, SaveCustomerProfileRequest request);
    Task<IReadOnlyList<CustomerAddressDto>> GetCustomerAddressesAsync(Guid customerId);
    Task<CustomerAddressDto?> CreateCustomerAddressAsync(Guid customerId, OutletPackageAddressRequest request);
    Task<OutletPackageQuoteDto?> QuoteAsync(OutletPackageQuoteRequest request);
    Task<SubscriptionDto?> CreateAsync(CreateOutletPackageRequest request);
    Task<SubscriptionDto?> MarkPaidAsync(Guid subscriptionId, MarkOutletPackagePaidRequest request);
    Task<SubscriptionDto?> ConfirmCustomerPackageAsync(Guid subscriptionId, ConfirmCustomerPackageRequest request);
    Task<SubscriptionDto?> AcceptAsync(Guid subscriptionId, AcceptOutletPackageRequest request, LegalAcceptanceContext? acceptanceContext = null);
}

public interface IOutletPackageActivationService
{
    Task<Subscription> ActivateAsync(Guid subscriptionId, string paymentMethod, Guid? paidByUserId);
}
