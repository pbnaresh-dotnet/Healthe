using HealthApp.Domain.Enums;
namespace HealthApp.Domain.Entities;

public sealed class User
{
    public Guid Id {
        get;
        set;
    }
    public string Email {
        get;
        set;
    }
    = "";
    public string PasswordHash {
        get;
        set;
    }
    = "";
    public string FirstName {
        get;
        set;
    }
    = "";
    public string LastName {
        get;
        set;
    }
    = "";
    public string? MobileNumber {
        get;
        set;
    }
    public UserRole Role {
        get;
        set;
    }
    public Guid? OutletId {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
    public bool IsDemo {
        get;
        set;
    }
    public DateTime? DemoExpiresAtUtc {
        get;
        set;
    }
    public CustomerProfile? CustomerProfile {
        get;
        set;
    }
}

public sealed class OutletDemoRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string RequestedBusinessName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? DemoExpiresAtUtc { get; set; }
    public string Status { get; set; } = "Requested";
}

public sealed class CustomerProfile
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public decimal? WeightKg {
        get;
        set;
    }
    public decimal? HeightCm {
        get;
        set;
    }
    public decimal? Bmi {
        get;
        set;
    }
    public DateTime? DateOfBirth {
        get;
        set;
    }
    public string Goal {
        get;
        set;
    }
    = "WeightLoss";
    public string ActivityLevel {
        get;
        set;
    }
    = "Moderate";
    public string Diet {
        get;
        set;
    }
    = "";
    public DateTime UpdatedAtUtc {
        get;
        set;
    }
    = DateTime.UtcNow;
}

public sealed class ServiceCity
{
    public Guid Id { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "India";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class CityArea
{
    public Guid Id {
        get;
        set;
    }
    public string City {
        get;
        set;
    }
    = "";
    public string State {
        get;
        set;
    }
    = "";
    public string Name {
        get;
        set;
    }
    = "";
    public string Pincode {
        get;
        set;
    }
    = "";
    public double Latitude {
        get;
        set;
    }
    public double Longitude {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class OutletDeliveryArea
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid CityAreaId {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class DeliveryPricingRule
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public decimal MaxDistanceKm {
        get;
        set;
    }
    public decimal Fee {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class CustomerAddress
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid? CityAreaId {
        get;
        set;
    }
    public string City {
        get;
        set;
    }
    = "";
    public string State {
        get;
        set;
    }
    = "";
    public string Pincode {
        get;
        set;
    }
    = "";
    public string Locality {
        get;
        set;
    }
    = "";
    public string Label {
        get;
        set;
    }
    = "";
    public string AddressLine1 {
        get;
        set;
    }
    = "";
    public string AddressLine2 {
        get;
        set;
    }
    = "";
    public string ContactName {
        get;
        set;
    }
    = "";
    public string ContactPhone {
        get;
        set;
    }
    = "";
    public double Latitude {
        get;
        set;
    }
    public double Longitude {
        get;
        set;
    }
    public bool IsDefault {
        get;
        set;
    }
}

public sealed class SubscriptionDiscountTier
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public int MinMeals {
        get;
        set;
    }
    public int? MaxMeals {
        get;
        set;
    }
    public decimal OneWeekPercent {
        get;
        set;
    }
    public decimal TwoWeeksPercent {
        get;
        set;
    }
    public decimal OneMonthPercent {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class MealSelectionHistory
{
    public Guid Id {
        get;
        set;
    }
    public Guid MealSelectionId {
        get;
        set;
    }
    public Guid SubscriptionId {
        get;
        set;
    }
    public string Action {
        get;
        set;
    }
    = "";
    public DateTime OccurredAtUtc {
        get;
        set;
    }
    = DateTime.UtcNow;
    public DateTime? FromMealDate {
        get;
        set;
    }
    public DateTime? ToMealDate {
        get;
        set;
    }
    public string Reason {
        get;
        set;
    }
    = "";
    public decimal Amount {
        get;
        set;
    }
}

public sealed class PaymentTransaction
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid? SubscriptionId {
        get;
        set;
    }
    public string Provider {
        get;
        set;
    }
    = "Mock";
    public string ProviderPaymentId {
        get;
        set;
    }
    = "";
    public string IdempotencyKey {
        get;
        set;
    }
    = "";
    public decimal Amount {
        get;
        set;
    }
    public string Currency {
        get;
        set;
    }
    = "INR";
    public string Status {
        get;
        set;
    }
    = "Pending";
    public DateTime CreatedAtUtc {
        get;
        set;
    }
    = DateTime.UtcNow;
    public DateTime? PaidAtUtc {
        get;
        set;
    }
}

public sealed class DiscountCode
{
    public Guid Id {
        get;
        set;
    }
    public Guid? OutletId {
        get;
        set;
    }
    public string Code {
        get;
        set;
    }
    = "";
    public decimal Percent {
        get;
        set;
    }
    public decimal? MaxAmount {
        get;
        set;
    }
    public int? MaxRedemptions {
        get;
        set;
    }
    public int RedemptionCount {
        get;
        set;
    }
    public DateTime? StartsAtUtc {
        get;
        set;
    }
    public DateTime? EndsAtUtc {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class OrderFinancialBreakdown
{
    public Guid Id {
        get;
        set;
    }
    public Guid OrderId {
        get;
        set;
    }
    public decimal GrossMealAmount {
        get;
        set;
    }
    public decimal DiscountAmount {
        get;
        set;
    }
    public decimal NetMealAmount {
        get;
        set;
    }
    public decimal DeliveryAmount {
        get;
        set;
    }
    public decimal PlatformServiceFee {
        get;
        set;
    }
    public decimal PlatformServiceGst {
        get;
        set;
    }
    public decimal RestaurantGstRate {
        get;
        set;
    }
    public decimal RestaurantTaxableAmount {
        get;
        set;
    }
    public GstMode RestaurantGstMode {
        get;
        set;
    } = GstMode.Exclusive;
    public decimal RestaurantGstAmount {
        get;
        set;
    }
    public decimal LateSkipFee {
        get;
        set;
    }
    public decimal CustomerPayable {
        get;
        set;
    }
    public decimal OutletCommission {
        get;
        set;
    }
    public decimal OutletCommissionGst {
        get;
        set;
    }
    public decimal OutletSettlementAmount {
        get;
        set;
    }
    public decimal HealthAppRevenue {
        get;
        set;
    }
}

public sealed class Outlet
{
    public Guid Id {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public string Slug {
        get;
        set;
    }
    = "";
    public string Subdomain {
        get;
        set;
    }
    = "";
    public string City {
        get;
        set;
    }
    = "";
    public string State {
        get;
        set;
    }
    = "";
    public string Pincode {
        get;
        set;
    }
    = "";
    public double Latitude {
        get;
        set;
    }
    public double Longitude {
        get;
        set;
    }
    public double ServiceRadiusKm {
        get;
        set;
    }
    public OutletStatus Status {
        get;
        set;
    }
    public string DeliveryDays { get; set; } = "";
    public BillingPlan BillingPlan {
        get;
        set;
    }
    public string LogoUrl {
        get;
        set;
    }
    = "";
    public string HeroImageUrl {
        get;
        set;
    }
    = "";
    public string HealthHighlights {
        get;
        set;
    }
    = "";
    public string PrimaryColor {
        get;
        set;
    }
    = "#14532d";
    public double Rating {
        get;
        set;
    }
    = 4.8;
    public int ReviewCount {
        get;
        set;
    }
    = 0;
    public string About {
        get;
        set;
    }
    = "";
    public int PreparationCutoffHours {
        get;
        set;
    }
    = 24;
    public bool AllowMealSkipping {
        get;
        set;
    }
    = true;
    public bool CreditDeliveryFeeOnSkip {
        get;
        set;
    }
    = true;
    public decimal RestaurantGstRate {
        get;
        set;
    } = 5m;
    public GstMode RestaurantGstMode {
        get;
        set;
    } = GstMode.Exclusive;
}

public sealed class SaaSPlan
{
    public Guid Id {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public decimal MonthlyFee {
        get;
        set;
    }
    public decimal AnnualFee {
        get;
        set;
    }
    public int IncludedActiveCustomers {
        get;
        set;
    }
    public decimal AdditionalCustomerFee {
        get;
        set;
    }
    public decimal CustomerTransactionFeePercent {
        get;
        set;
    }
    public bool IsActive {
        get;
        set;
    }
    = true;
    public string Description {
        get;
        set;
    }
    = "";
}

public sealed class OutletSubscription
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid SaaSPlanId {
        get;
        set;
    }
    public string BillingCycle {
        get;
        set;
    }
    = "Monthly";
    public decimal SubscriptionFee {
        get;
        set;
    }
    public decimal SetupFee {
        get;
        set;
    }
    public decimal TransactionFeePercent {
        get;
        set;
    }
    public DateTime StartDate {
        get;
        set;
    }
    public DateTime RenewalDate {
        get;
        set;
    }
    public string Status {
        get;
        set;
    }
    = "Active";
}


public sealed class OutletOnboardingApplication
{
    public Guid Id { get; set; }
    public string AccessKeyHash { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string AccountFirstName { get; set; } = "";
    public string AccountLastName { get; set; } = "";
    public Guid SaaSPlanId { get; set; }
    public string PlanName { get; set; } = "";
    public string BillingCycle { get; set; } = "Monthly";
    public decimal SubscriptionFee { get; set; }
    public decimal SetupFee { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
    public string PaymentReference { get; set; } = "";
    public string Status { get; set; } = "Onboarding";
    public string BusinessType { get; set; } = "Individual";
    public string OutletName { get; set; } = "";
    public string Description { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Pincode { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string OwnerEmail { get; set; } = "";
    public string OwnerPhone { get; set; } = "";
    public string AadhaarNumber { get; set; } = "";
    public string AadhaarCardUrl { get; set; } = "";
    public string AadhaarCardFileName { get; set; } = "";
    public string BusinessRegistrationUrl { get; set; } = "";
    public string BusinessRegistrationFileName { get; set; } = "";
    public string BusinessPan { get; set; } = "";
    public string BusinessPanDocumentUrl { get; set; } = "";
    public string BusinessPanDocumentFileName { get; set; } = "";
    public string GstNumber { get; set; } = "";
    public string GstCertificateUrl { get; set; } = "";
    public string GstCertificateFileName { get; set; } = "";
    public string VerificationNotes { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public Guid? OutletId { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class MealPlan
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public string Frequency {
        get;
        set;
    }
    = "Weekly";
    public int MealsPerDay {
        get;
        set;
    }
    public int MealsPerWeek {
        get;
        set;
    }
    public decimal Price {
        get;
        set;
    }
    public string Currency {
        get;
        set;
    }
    = "INR";
    public string Description {
        get;
        set;
    }
    = "";
    public bool IsActive {
        get;
        set;
    }
    = true;
}

public sealed class Recipe
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public int Calories {
        get;
        set;
    }
    public int ProteinGrams {
        get;
        set;
    }
    public int CarbsGrams {
        get;
        set;
    }
    public int FatGrams {
        get;
        set;
    }
    public int FiberGrams {
        get;
        set;
    }
    public RecipeCategory Category {
        get;
        set;
    }
    public decimal PricePerMeal {
        get;
        set;
    }
    public decimal LargePricePerMeal {
        get;
        set;
    }
    public string Description {
        get;
        set;
    }
    = "";
    public string ImageUrl {
        get;
        set;
    }
    = "";
    public string Tags {
        get;
        set;
    }
    = "";
    public bool IsActive {
        get;
        set;
    }
    = true;
    public ICollection<RecipeIngredient> RecipeIngredients {
        get;
        set;
    }
    = new List<RecipeIngredient>();
    public ICollection<RecipeAllergen> RecipeAllergens {
        get;
        set;
    }
    = new List<RecipeAllergen>();
}

public sealed class Ingredient
{
    public Guid Id {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public string DefaultUnit {
        get;
        set;
    }
    = "g";
    public bool IsActive {
        get;
        set;
    }
    = true;
    public ICollection<IngredientAllergen> Allergens {
        get;
        set;
    }
    = new List<IngredientAllergen>();
}

public sealed class Allergen
{
    public Guid Id {
        get;
        set;
    }
    public string Name {
        get;
        set;
    }
    = "";
    public bool IsActive {
        get;
        set;
    }
    = true;
    public ICollection<IngredientAllergen> Ingredients {
        get;
        set;
    }
    = new List<IngredientAllergen>();
    public ICollection<RecipeAllergen> Recipes {
        get;
        set;
    }
    = new List<RecipeAllergen>();
    public ICollection<CustomerAllergy> Customers {
        get;
        set;
    }
    = new List<CustomerAllergy>();
}

public sealed class RecipeIngredient
{
    public Guid Id {
        get;
        set;
    }
    public Guid RecipeId {
        get;
        set;
    }
    public Guid IngredientId {
        get;
        set;
    }
    public decimal Quantity {
        get;
        set;
    }
    public string Unit {
        get;
        set;
    }
    = "g";
    public Recipe Recipe {
        get;
        set;
    }
    = null!;
    public Ingredient Ingredient {
        get;
        set;
    }
    = null!;
}

public sealed class RecipeAllergen
{
    public Guid RecipeId {
        get;
        set;
    }
    public Guid AllergenId {
        get;
        set;
    }
    public Recipe Recipe {
        get;
        set;
    }
    = null!;
    public Allergen Allergen {
        get;
        set;
    }
    = null!;
}

public sealed class IngredientAllergen
{
    public Guid IngredientId {
        get;
        set;
    }
    public Guid AllergenId {
        get;
        set;
    }
    public Ingredient Ingredient {
        get;
        set;
    }
    = null!;
    public Allergen Allergen {
        get;
        set;
    }
    = null!;
}

public sealed class CustomerAllergy
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid AllergenId {
        get;
        set;
    }
    public User Customer {
        get;
        set;
    }
    = null!;
    public Allergen Allergen {
        get;
        set;
    }
    = null!;
}

public sealed class CustomerLikedMeal
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class OutletMenuItem
{
    public Guid Id {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid RecipeId {
        get;
        set;
    }
    public DayOfWeek DayOfWeek {
        get;
        set;
    }
    public MealSlot MealSlot {
        get;
        set;
    }
    public bool IsAvailable {
        get;
        set;
    }
    = true;
    public int DisplayOrder {
        get;
        set;
    }
}

public sealed class Subscription
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public string DeliveryCity {
        get;
        set;
    }
    = "";
    public Guid MealPlanId {
        get;
        set;
    }
    public string PlanName {
        get;
        set;
    }
    = "";
    public SubscriptionDeliveryMode DeliveryMode {
        get;
        set;
    }
    public SubscriptionDuration Duration {
        get;
        set;
    }
    = SubscriptionDuration.OneWeek;
    public DateTime StartDate {
        get;
        set;
    }
    public DateTime EndDate {
        get;
        set;
    }
    public decimal GrossMealAmount {
        get;
        set;
    }
    public decimal SubscriptionDiscountPercent {
        get;
        set;
    }
    public decimal SubscriptionDiscountAmount {
        get;
        set;
    }
    public decimal NetMealAmount {
        get;
        set;
    }
    public decimal PlatformServiceFee {
        get;
        set;
    }
    public decimal PlatformServiceGst {
        get;
        set;
    }
    public decimal RestaurantGstRate {
        get;
        set;
    }
    public GstMode RestaurantGstMode {
        get;
        set;
    } = GstMode.Exclusive;
    public decimal RestaurantTaxableAmount {
        get;
        set;
    }
    public decimal RestaurantGstAmount {
        get;
        set;
    }
    public decimal LateSkipFee {
        get;
        set;
    }
    public decimal Price {
        get;
        set;
    }
    public decimal DeliveryFee {
        get;
        set;
    }
    public decimal CustomerTransactionFeePercent {
        get;
        set;
    }
    public decimal TransactionFee {
        get;
        set;
    }
    public decimal TotalCharged {
        get;
        set;
    }
    public decimal OutletAmount {
        get;
        set;
    }
    public decimal PlatformServiceFeePercent {
        get;
        set;
    }
    public decimal PlatformServiceGstRate {
        get;
        set;
    }
    public decimal OutletCommissionPercent {
        get;
        set;
    }
    public decimal OutletCommissionAmount {
        get;
        set;
    }
    public string? DiscountCode {
        get;
        set;
    }
    public decimal DiscountCodeAmount {
        get;
        set;
    }
    public int TotalMealCount {
        get;
        set;
    }
    public string Frequency {
        get;
        set;
    }
    = "Weekly";
    public int MealsPerDay {
        get;
        set;
    }
    public int MealsPerWeek {
        get;
        set;
    }
    public SubscriptionStatus Status {
        get;
        set;
    }
    // Outlet-created package workflow snapshot.
    public string PackageStatus {
        get;
        set;
    } = "Active";
    public bool IsOutletCreated {
        get;
        set;
    }
    public Guid? CreatedByOutletUserId {
        get;
        set;
    }
    public OutletPackageDiscountType OutletDiscountType {
        get;
        set;
    } = OutletPackageDiscountType.None;
    public decimal OutletDiscountValue {
        get;
        set;
    }
    public string OutletDiscountReason {
        get;
        set;
    } = "";
    public string PaymentMethod {
        get;
        set;
    } = "Online";
    public Guid? PaidByUserId {
        get;
        set;
    }
    public DateTime? PaidAtUtc {
        get;
        set;
    }
    public DateTime? AcceptedAtUtc {
        get;
        set;
    }
    public DateTime? SentAtUtc {
        get;
        set;
    }
    public DateTime NextDeliveryDate {
        get;
        set;
    }
}

public sealed class SubscriptionMealSelection
{
    public Guid Id {
        get;
        set;
    }
    public Guid SubscriptionId {
        get;
        set;
    }
    public DateTime MealDate {
        get;
        set;
    }
    public MealSlot MealSlot {
        get;
        set;
    }
    public Guid RecipeId {
        get;
        set;
    }
    public Guid? AddressId {
        get;
        set;
    }
    public MealPortionSize PortionSize {
        get;
        set;
    }
    = MealPortionSize.Regular;
    public MealSelectionStatus Status {
        get;
        set;
    }
    = MealSelectionStatus.Scheduled;
    public decimal MealPrice {
        get;
        set;
    }
    public decimal DeliveryFee {
        get;
        set;
    }
    public DateTime? SkippedAtUtc {
        get;
        set;
    }
    public decimal LateSkipFee {
        get;
        set;
    }
    public DateTime? RescheduledAtUtc {
        get;
        set;
    }
    public DateTime? OriginalMealDate {
        get;
        set;
    }
    public Guid? RescheduledFromSelectionId {
        get;
        set;
    }
}

public sealed class CustomerCreditTransaction
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid? SubscriptionId {
        get;
        set;
    }
    public Guid? MealSelectionId {
        get;
        set;
    }
    public decimal Amount {
        get;
        set;
    }
    public CreditTransactionType Type {
        get;
        set;
    }
    public string Reason {
        get;
        set;
    }
    = "";
    public DateTime CreatedAt {
        get;
        set;
    }
    = DateTime.UtcNow;
}

public sealed class Order
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid SubscriptionId {
        get;
        set;
    }
    public decimal Total {
        get;
        set;
    }
    public OrderStatus Status {
        get;
        set;
    }
    public DateTime DeliveryDate {
        get;
        set;
    }
    public string Address {
        get;
        set;
    }
    = "";
    public Guid? DeliveryAddressId {
        get;
        set;
    }
}

public sealed class Delivery
{
    public Guid? DeliveryAddressId {
        get;
        set;
    }
    public Guid? RouteId { get; set; }
    public Guid? RouteStopId { get; set; }
    public int? RouteSequence { get; set; }
    public Guid Id {
        get;
        set;
    }
    public Guid OrderId {
        get;
        set;
    }
    public Guid SubscriptionId {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public DateTime ScheduledDate {
        get;
        set;
    }
    public MealSlot MealSlot {
        get;
        set;
    }
    public string CustomerName {
        get;
        set;
    }
    = "";
    public string Address {
        get;
        set;
    }
    = "";
    public decimal DeliveryFee {
        get;
        set;
    }
    public DeliveryStatus Status {
        get;
        set;
    }
}

public sealed class DeliveryRoute
{
    public Guid Id { get; set; }
    public Guid OutletId { get; set; }
    public Guid DriverId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public MealSlot MealSlot { get; set; } = MealSlot.Morning;
    public RouteStatus Status { get; set; } = RouteStatus.Planned;
    public double TotalDistanceKm { get; set; }
    public double TotalDurationMinutes { get; set; }
    public string RoutingSource { get; set; } = "OSRM";
    public string GeometryJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DeliveryRouteStop> Stops { get; set; } = new List<DeliveryRouteStop>();
}

public sealed class DeliveryRouteStop
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public int StopSequence { get; set; }
    public MealSlot MealSlot { get; set; } = MealSlot.Morning;
    public Guid DeliveryAddressId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int DeliveryCount { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Scheduled;
}

public sealed class PlatformTransaction
{
    public Guid Id {
        get;
        set;
    }
    public Guid CustomerId {
        get;
        set;
    }
    public Guid OutletId {
        get;
        set;
    }
    public Guid? SubscriptionId {
        get;
        set;
    }
    public string Type {
        get;
        set;
    }
    = "CustomerSubscription";
    public string? ReferenceId {
        get;
        set;
    }
    public decimal GrossAmount {
        get;
        set;
    }
    public decimal PlatformFee {
        get;
        set;
    }
    public decimal OutletAmount {
        get;
        set;
    }
    public decimal FeePercent {
        get;
        set;
    }
    public string Currency {
        get;
        set;
    }
    = "INR";
    public string Status {
        get;
        set;
    }
    = "Paid";
    public DateTime CreatedAt {
        get;
        set;
    }
    = DateTime.UtcNow;
}
