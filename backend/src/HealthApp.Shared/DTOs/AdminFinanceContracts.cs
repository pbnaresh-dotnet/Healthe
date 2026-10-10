namespace HealthApp.Shared.DTOs;

public record AdminFinanceReportRequest(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? OutletGroupId = null,
    Guid? OutletId = null,
    string? City = null,
    Guid? MealPlanId = null,
    string Section = "summary",
    int Page = 1,
    int PageSize = 25);

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
    IReadOnlyList<AdminFinanceDailyRowDto> Daily,
    string Section = "summary",
    int Page = 1,
    int PageSize = 25,
    int TotalRows = 0);


public record PaymentSettlementReconciliationDto(
    Guid Id,
    Guid PaymentTransactionId,
    Guid? OutletId,
    string Provider,
    string ProviderPaymentId,
    string ProviderSettlementId,
    decimal GrossAmount,
    decimal GatewayFeeAmount,
    decimal GatewayFeeTaxAmount,
    decimal OtherProviderAdjustmentAmount,
    decimal NetSettlementAmount,
    string Currency,
    string Status,
    string ReconciliationReference,
    DateTime? SettledAtUtc,
    DateTime? ReconciledAtUtc,
    string ReconciledBy);

public record RecordPaymentSettlementRequest(
    Guid PaymentTransactionId,
    string ProviderSettlementId,
    decimal GatewayFeeAmount,
    decimal GatewayFeeTaxAmount,
    decimal OtherProviderAdjustmentAmount = 0m,
    DateTime? SettledAtUtc = null,
    string? ReconciliationReference = null,
    string? ReconciledBy = null,
    string? SourceDataJson = null);

public record PaymentSettlementImportResultDto(
    string Provider,
    int TotalRows,
    int ReconciledRows,
    int AlreadyReconciledRows,
    int UnmatchedRows,
    IReadOnlyList<string> Errors)
{
    // Backward-compatible name for consumers that display import exception rows.
    public IReadOnlyList<string> ExceptionRows => Errors;
}


public record PaymentSettlementReconciliationExceptionDto(
    Guid Id,
    string Provider,
    string ProviderPaymentId,
    string ProviderSettlementId,
    string ExceptionType,
    string Status,
    decimal? ReportedGrossAmount,
    decimal? ReportedNetSettlementAmount,
    string Currency,
    string ErrorMessage,
    string AssignedTo,
    string ResolutionNotes,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string ResolvedBy);

public record ResolvePaymentSettlementExceptionRequest(
    string ResolutionNotes,
    string? ResolvedBy = null);