namespace HealthApp.Shared.DTOs;

public record CreateOutletCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    decimal? WeightKg = null,
    decimal? HeightCm = null,
    DateTime? DateOfBirth = null,
    string Goal = "WeightLoss",
    string ActivityLevel = "Moderate",
    string Diet = "",
    IReadOnlyList<Guid>? AllergyIds = null);

public record OutletCustomerProfileDto(
    UserDto Customer,
    CustomerProfileDto? Profile,
    IReadOnlyList<CustomerAddressDto> Addresses);

public record OutletPackageQuoteRequest(
    Guid CustomerId,
    Guid OutletId,
    string DeliveryMode,
    string Duration,
    IReadOnlyList<MealSelectionItem> Selections,
    string DiscountType = "None",
    decimal DiscountValue = 0m,
    string DiscountReason = "",
    IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null,
    string? DeliveryCity = null);

public record OutletPackageQuoteDto(
    Guid CustomerId,
    decimal GrossMealAmount,
    decimal DiscountAmount,
    decimal DiscountPercent,
    string DiscountType,
    decimal RestaurantTaxableAmount,
    decimal RestaurantGstAmount,
    decimal RestaurantGstRate,
    string RestaurantGstMode,
    decimal DeliveryFee,
    decimal PlatformServiceFee,
    decimal PlatformServiceGst,
    decimal OutletCommissionAmount,
    decimal OutletSettlementAmount,
    decimal TotalCharged,
    IReadOnlyList<DeliveryQuoteDto> DeliveryQuotes,
    IReadOnlyList<AllergyWarningDto> AllergyWarnings,
    bool RequiresAllergyConfirmation);

public record CreateOutletPackageRequest(
    Guid CustomerId,
    string DeliveryMode,
    string Duration,
    IReadOnlyList<MealSelectionItem> Selections,
    string DiscountType = "None",
    decimal DiscountValue = 0m,
    string DiscountReason = "",
    IReadOnlyList<Guid>? ConfirmedAllergyRecipeIds = null,
    string? DeliveryCity = null);

public record MarkOutletPackagePaidRequest(
    string PaymentMethod = "Cash",
    string Note = "");

public record OutletPackageAddressRequest(
    string City,
    string Pincode,
    string Locality,
    string Label,
    string AddressLine1,
    string AddressLine2,
    string ContactName,
    string ContactPhone,
    double Latitude,
    double Longitude,
    Guid? CityAreaId = null,
    bool IsDefault = false);
