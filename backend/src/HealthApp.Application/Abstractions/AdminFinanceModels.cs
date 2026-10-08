using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Abstractions;

public sealed record AdminFinanceSubscriptionRow(
    Guid SubscriptionId,
    Guid OutletId,
    string OutletName,
    string City,
    Guid? OutletGroupId,
    string GroupName,
    Guid MealPlanId,
    string PlanName,
    DateTime StartDate,
    DateTime? PaidAtUtc,
    decimal GrossMealAmount,
    decimal SubscriptionDiscountAmount,
    decimal NetMealAmount,
    decimal DeliveryFee,
    decimal TotalCharged,
    decimal RestaurantTaxableAmount,
    decimal RestaurantGstAmount,
    decimal PlatformServiceFee,
    decimal PlatformServiceGst,
    decimal OutletCommissionAmount,
    decimal LateSkipFee,
    decimal OutletAmount,
    bool IsPaid);

public interface IAdminFinanceRepository
{
    Task<IReadOnlyList<AdminFinanceSubscriptionRow>> GetSubscriptionsAsync(
        AdminFinanceReportRequest request,
        CancellationToken cancellationToken = default);
}
