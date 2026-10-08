using HealthApp.Application.Abstractions;
using HealthApp.Infrastructure.Data;
using HealthApp.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class AdminFinanceRepository(HealthAppDbContext db) : IAdminFinanceRepository
{
    public async Task<IReadOnlyList<AdminFinanceSubscriptionRow>> GetSubscriptionsAsync(
        AdminFinanceReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var from = (request.FromDate ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var toExclusive = (request.ToDate ?? DateTime.UtcNow.Date).Date.AddDays(1);

        if (toExclusive <= from)
            return [];

        var query =
            from s in db.Subscriptions.AsNoTracking()
            join o in db.Outlets.AsNoTracking() on s.OutletId equals o.Id
            where s.StartDate >= from && s.StartDate < toExclusive
            select new
            {
                Subscription = s,
                OutletId = o.Id,
                OutletName = o.Name,
                City = o.City,
                OutletGroupId = o.OutletGroupId
            };

        if (request.OutletGroupId.HasValue)
            query = query.Where(x => x.OutletGroupId == request.OutletGroupId.Value);

        if (request.OutletId.HasValue)
            query = query.Where(x => x.OutletId == request.OutletId.Value);

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim();
            query = query.Where(x => x.City == city);
        }

        if (request.MealPlanId.HasValue)
            query = query.Where(x => x.Subscription.MealPlanId == request.MealPlanId.Value);

        var rows = await query
            .OrderBy(x => x.Subscription.StartDate)
            .ThenBy(x => x.OutletName)
            .ThenBy(x => x.Subscription.PlanName)
            .ToListAsync(cancellationToken);

        var groupIds = rows
            .Where(x => x.OutletGroupId.HasValue)
            .Select(x => x.OutletGroupId!.Value)
            .Distinct()
            .ToList();

        var groupNames = groupIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.OutletGroups.AsNoTracking()
                .Where(x => groupIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return rows.Select(x =>
        {
            var s = x.Subscription;
            var groupName = x.OutletGroupId.HasValue && groupNames.TryGetValue(x.OutletGroupId.Value, out var name)
                ? name
                : "";

            return new AdminFinanceSubscriptionRow(
                s.Id,
                x.OutletId,
                x.OutletName,
                x.City,
                x.OutletGroupId,
                groupName,
                s.MealPlanId,
                s.PlanName,
                s.StartDate,
                s.PaidAtUtc,
                s.GrossMealAmount,
                s.SubscriptionDiscountAmount + s.DiscountCodeAmount,
                s.NetMealAmount,
                s.DeliveryFee,
                s.TotalCharged,
                s.RestaurantTaxableAmount,
                s.RestaurantGstAmount,
                s.PlatformServiceFee,
                s.PlatformServiceGst,
                s.OutletCommissionAmount,
                s.LateSkipFee,
                s.OutletAmount,
                s.PaidAtUtc.HasValue);
        }).ToList();
    }
}
