namespace HealthApp.Shared.DTOs;

public record AdminFinanceReportRequest(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? OutletGroupId = null,
    Guid? OutletId = null,
    string? City = null,
    Guid? MealPlanId = null);

public record AdminFinanceAmountsDto(
    decimal GrossMealAmount,
    decimal DiscountAmount,
    decimal NetMealAmount,
    decimal DeliveryFee,
    decimal CustomerCharges,
    decimal RestaurantTaxableAmount,
    decimal RestaurantGstAmount,
    decimal PlatformServiceFee,
    decimal PlatformServiceGst,
    decimal OutletCommissionAmount,
    decimal LateSkipFee,
    decimal OutletSettlementAmount,
    decimal HealthAppRevenue);

public record AdminFinanceReportTotalsDto(
    int SubscriptionCount,
    int PaidSubscriptionCount,
    int PendingSubscriptionCount,
    AdminFinanceAmountsDto AllSubscriptions,
    AdminFinanceAmountsDto PaidSubscriptions);

public record AdminFinanceOutletRowDto(
    Guid OutletId,
    string OutletName,
    string City,
    Guid? OutletGroupId,
    string GroupName,
    int SubscriptionCount,
    int PaidSubscriptionCount,
    AdminFinanceAmountsDto Amounts);

public record AdminFinanceGroupRowDto(
    Guid? OutletGroupId,
    string GroupName,
    int OutletCount,
    int SubscriptionCount,
    int PaidSubscriptionCount,
    AdminFinanceAmountsDto Amounts);

public record AdminFinanceDailyRowDto(
    DateTime Date,
    Guid? OutletGroupId,
    string GroupName,
    int SubscriptionCount,
    int PaidSubscriptionCount,
    AdminFinanceAmountsDto Amounts);

public record AdminFinanceReportDto(
    DateTime FromDate,
    DateTime ToDate,
    AdminFinanceReportTotalsDto Totals,
    IReadOnlyList<AdminFinanceOutletRowDto> Outlets,
    IReadOnlyList<AdminFinanceGroupRowDto> Groups,
    IReadOnlyList<AdminFinanceDailyRowDto> Daily);
