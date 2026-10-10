using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Infrastructure.Data;
using HealthApp.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class AdminFinanceRepository(HealthAppDbContext db) : IAdminFinanceRepository
{
    private IQueryable<AdminFinanceSubscriptionRow> FilteredRows(AdminFinanceReportRequest request)
    {
        var fromDate = (request.FromDate ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var toExclusiveDate = (request.ToDate ?? DateTime.UtcNow.Date).Date.AddDays(1);
        var query = from s in db.Subscriptions.AsNoTracking()
                    join o in db.Outlets.AsNoTracking() on s.OutletId equals o.Id
                    join g0 in db.OutletGroups.AsNoTracking() on o.OutletGroupId equals g0.Id into groupJoin
                    from g in groupJoin.DefaultIfEmpty()
                    where s.StartDate >= fromDate && s.StartDate < toExclusiveDate
                    select new { s, o, g };

        if (request.OutletGroupId.HasValue)
            query = query.Where(x => x.o.OutletGroupId == request.OutletGroupId.Value);
        if (request.OutletId.HasValue)
            query = query.Where(x => x.o.Id == request.OutletId.Value);
        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim();
            query = query.Where(x => x.o.City == city);
        }
        if (request.MealPlanId.HasValue)
            query = query.Where(x => x.s.MealPlanId == request.MealPlanId.Value);

        return query.Select(x => new AdminFinanceSubscriptionRow(
            x.s.Id, x.o.Id, x.o.Name, x.o.City, x.o.OutletGroupId, x.g == null ? "" : x.g.Name,
            x.s.MealPlanId, x.s.PlanName, x.s.StartDate, x.s.PaidAtUtc,
            x.s.GrossMealAmount, x.s.SubscriptionDiscountAmount + x.s.DiscountCodeAmount,
            x.s.NetMealAmount, x.s.DeliveryFee, x.s.TotalCharged, x.s.RestaurantTaxableAmount,
            x.s.RestaurantGstAmount, x.s.PlatformServiceFee, x.s.PlatformServiceGst,
            x.s.OutletCommissionAmount, x.s.LateSkipFee, x.s.OutletAmount, x.s.PaidAtUtc.HasValue));
    }

    private static IQueryable<AdminFinanceAmountsDto> Amounts(IQueryable<AdminFinanceSubscriptionRow> rows) =>
        rows.GroupBy(_ => 1).Select(g => new AdminFinanceAmountsDto(
            g.Sum(x => x.GrossMealAmount), g.Sum(x => x.SubscriptionDiscountAmount),
            g.Sum(x => x.NetMealAmount), g.Sum(x => x.DeliveryFee), g.Sum(x => x.TotalCharged),
            g.Sum(x => x.RestaurantTaxableAmount), g.Sum(x => x.RestaurantGstAmount),
            g.Sum(x => x.PlatformServiceFee), g.Sum(x => x.PlatformServiceGst),
            g.Sum(x => x.OutletCommissionAmount), g.Sum(x => x.LateSkipFee), g.Sum(x => x.OutletAmount),
            g.Sum(x => x.PlatformServiceFee + x.OutletCommissionAmount + x.LateSkipFee)));

    public async Task<AdminFinanceReportDto> GetReportAsync(
        AdminFinanceReportRequest request, string section, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        section = (section ?? "summary").Trim().ToLowerInvariant();
        if (section is not ("summary" or "outlets" or "groups" or "daily"))
            throw new ArgumentException("Unsupported finance section. Use summary, outlets, groups or daily.");

        var rows = FilteredRows(request);
        var paidRows = rows.Where(x => x.IsPaid);
        var subscriptionCount = await rows.CountAsync(cancellationToken);
        var paidCount = await paidRows.CountAsync(cancellationToken);
        var allAmounts = await Amounts(rows).FirstOrDefaultAsync(cancellationToken)
            ?? new AdminFinanceAmountsDto(0,0,0,0,0,0,0,0,0,0,0,0,0);
        var paidAmounts = await Amounts(paidRows).FirstOrDefaultAsync(cancellationToken)
            ?? new AdminFinanceAmountsDto(0,0,0,0,0,0,0,0,0,0,0,0,0);

        var outlets = new List<AdminFinanceOutletRowDto>();
        var groups = new List<AdminFinanceGroupRowDto>();
        var daily = new List<AdminFinanceDailyRowDto>();
        var totalRows = 0;

        if (section == "outlets")
        {
            var grouped = rows.GroupBy(x => new { x.OutletId, x.OutletName, x.City, x.OutletGroupId, x.GroupName });
            totalRows = await grouped.CountAsync(cancellationToken);
            outlets = await grouped.OrderBy(g => g.Key.OutletName).ThenBy(g => g.Key.OutletId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new AdminFinanceOutletRowDto(
                    g.Key.OutletId, g.Key.OutletName, g.Key.City, g.Key.OutletGroupId,
                    g.Key.GroupName == "" ? "Unassigned" : g.Key.GroupName,
                    g.Count(), g.Count(x => x.IsPaid),
                    new AdminFinanceAmountsDto(
                        g.Sum(x => x.GrossMealAmount), g.Sum(x => x.SubscriptionDiscountAmount),
                        g.Sum(x => x.NetMealAmount), g.Sum(x => x.DeliveryFee), g.Sum(x => x.TotalCharged),
                        g.Sum(x => x.RestaurantTaxableAmount), g.Sum(x => x.RestaurantGstAmount),
                        g.Sum(x => x.PlatformServiceFee), g.Sum(x => x.PlatformServiceGst),
                        g.Sum(x => x.OutletCommissionAmount), g.Sum(x => x.LateSkipFee), g.Sum(x => x.OutletAmount),
                        g.Sum(x => x.PlatformServiceFee + x.OutletCommissionAmount + x.LateSkipFee))))
                .ToListAsync(cancellationToken);
        }
        else if (section == "groups")
        {
            var grouped = rows.GroupBy(x => new { x.OutletGroupId, x.GroupName });
            totalRows = await grouped.CountAsync(cancellationToken);
            groups = await grouped.OrderBy(g => g.Key.GroupName).ThenBy(g => g.Key.OutletGroupId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new AdminFinanceGroupRowDto(
                    g.Key.OutletGroupId,
                    g.Key.GroupName == "" ? "Unassigned" : g.Key.GroupName,
                    g.Select(x => x.OutletId).Distinct().Count(), g.Count(), g.Count(x => x.IsPaid),
                    new AdminFinanceAmountsDto(
                        g.Sum(x => x.GrossMealAmount), g.Sum(x => x.SubscriptionDiscountAmount),
                        g.Sum(x => x.NetMealAmount), g.Sum(x => x.DeliveryFee), g.Sum(x => x.TotalCharged),
                        g.Sum(x => x.RestaurantTaxableAmount), g.Sum(x => x.RestaurantGstAmount),
                        g.Sum(x => x.PlatformServiceFee), g.Sum(x => x.PlatformServiceGst),
                        g.Sum(x => x.OutletCommissionAmount), g.Sum(x => x.LateSkipFee), g.Sum(x => x.OutletAmount),
                        g.Sum(x => x.PlatformServiceFee + x.OutletCommissionAmount + x.LateSkipFee))))
                .ToListAsync(cancellationToken);
        }
        else if (section == "daily")
        {
            var grouped = rows.GroupBy(x => new { Date = x.StartDate.Date, x.OutletGroupId, x.GroupName });
            totalRows = await grouped.CountAsync(cancellationToken);
            daily = await grouped.OrderBy(g => g.Key.Date).ThenBy(g => g.Key.GroupName).ThenBy(g => g.Key.OutletGroupId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new AdminFinanceDailyRowDto(
                    g.Key.Date, g.Key.OutletGroupId,
                    g.Key.GroupName == "" ? "Unassigned" : g.Key.GroupName,
                    g.Count(), g.Count(x => x.IsPaid),
                    new AdminFinanceAmountsDto(
                        g.Sum(x => x.GrossMealAmount), g.Sum(x => x.SubscriptionDiscountAmount),
                        g.Sum(x => x.NetMealAmount), g.Sum(x => x.DeliveryFee), g.Sum(x => x.TotalCharged),
                        g.Sum(x => x.RestaurantTaxableAmount), g.Sum(x => x.RestaurantGstAmount),
                        g.Sum(x => x.PlatformServiceFee), g.Sum(x => x.PlatformServiceGst),
                        g.Sum(x => x.OutletCommissionAmount), g.Sum(x => x.LateSkipFee), g.Sum(x => x.OutletAmount),
                        g.Sum(x => x.PlatformServiceFee + x.OutletCommissionAmount + x.LateSkipFee))))
                .ToListAsync(cancellationToken);
        }

        var from = (request.FromDate ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var to = (request.ToDate ?? DateTime.UtcNow.Date).Date;
        return new AdminFinanceReportDto(from, to,
            new AdminFinanceReportTotalsDto(subscriptionCount, paidCount, subscriptionCount - paidCount, allAmounts, paidAmounts),
            outlets, groups, daily, section, page, pageSize, totalRows);
    }

}
