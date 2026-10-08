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
            join g0 in db.OutletGroups.AsNoTracking() on o.OutletGroupId equals g0.Id into groups
            from g in groups.DefaultIfEmpty()
            where s.StartDate >= from && s.StartDate < toExclusive
            select new AdminFinanceSubscriptionRow(
                s.Id,
                s.OutletId,
                o.Name,
                o.City,
                o.OutletGroupId,
                g == null ? "" : g.Name,
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
            query = query.Where(x => x.MealPlanId == request.MealPlanId.Value);

        return await query
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.OutletName)
            .ThenBy(x => x.PlanName)
            .ToListAsync(cancellationToken);
    }
}
