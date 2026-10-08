using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using HealthApp.Domain.Entities;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class AdminOutletLifecycleService(
    IOutletRepository outlets,
    IOutletSubscriptionRepository outletSubscriptions,
    ISaaSPlanRepository saasPlans,
    ITrialRepository trials,
    ICurrentUser currentUser) : IAdminOutletLifecycleService
{
    public async Task<AdminOutletReactivationOptionsDto?> GetReactivationOptionsAsync(Guid outletId)
    {
        _ = await outlets.GetByIdAsync(outletId)
            ?? throw new KeyNotFoundException("Outlet not found.");

        var subscription = await outletSubscriptions.GetAnyByOutletAsync(outletId);
        var trial = await trials.GetByOutletAsync(outletId);
        var plans = (await saasPlans.GetActiveAsync())
            .Select(x => new SaaSPlanDto(
                x.Id,
                x.Name,
                x.MonthlyFee,
                x.AnnualFee,
                x.IncludedActiveCustomers,
                x.AdditionalCustomerFee,
                x.CustomerTransactionFeePercent,
                x.Description,
                x.IsActive))
            .ToList();

        var subscriptionStatus = subscription?.Status ?? "None";
        var trialStatus = trial?.Status.ToString() ?? "None";
        var canReactivate =
            string.Equals(subscriptionStatus, "Expired", StringComparison.OrdinalIgnoreCase) ||
            trial?.Status == TrialStatus.Expired;

        var outlet = await outlets.GetByIdAsync(outletId)!;
        return new AdminOutletReactivationOptionsDto(
            outletId,
            outlet!.Status.ToString(),
            subscriptionStatus,
            trialStatus,
            subscription?.SaaSPlanId,
            plans,
            canReactivate);
    }

    public async Task<AdminOutletReactivationDto?> ReactivateAsync(
        Guid outletId,
        AdminOutletReactivationRequest request)
    {
        if (currentUser.UserId is not Guid adminId)
            throw new UnauthorizedAccessException("The current Super Admin identity could not be determined.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reactivation reason is required.");

        var outlet = await outlets.GetByIdAsync(outletId)
            ?? throw new KeyNotFoundException("Outlet not found.");

        var plan = await saasPlans.GetAsync(request.SaaSPlanId)
            ?? throw new KeyNotFoundException("SaaS plan not found.");

        if (!plan.IsActive)
            throw new InvalidOperationException("The selected SaaS plan is not active.");

        var cycle = request.BillingCycle?.Trim();
        if (!string.Equals(cycle, "Monthly", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(cycle, "Annual", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Billing cycle must be Monthly or Annual.");

        var subscription = await outletSubscriptions.GetAnyByOutletAsync(outletId);
        var trial = await trials.GetByOutletAsync(outletId);

        if (subscription is not null &&
            !string.Equals(subscription.Status, "Expired", StringComparison.OrdinalIgnoreCase) &&
            trial?.Status != TrialStatus.Expired)
            throw new InvalidOperationException(
                $"Outlet subscription is '{subscription.Status}', so this reactivation action is not applicable.");

        var now = DateTime.UtcNow;
        var normalizedCycle = string.Equals(cycle, "Annual", StringComparison.OrdinalIgnoreCase)
            ? "Annual"
            : "Monthly";
        var renewal = normalizedCycle == "Annual"
            ? now.Date.AddYears(1)
            : now.Date.AddMonths(1);
        var fee = normalizedCycle == "Annual" ? plan.AnnualFee : plan.MonthlyFee;

        if (subscription is null)
        {
            subscription = new OutletSubscription
            {
                Id = Guid.NewGuid(),
                OutletId = outletId,
                SaaSPlanId = plan.Id,
                BillingCycle = normalizedCycle,
                SubscriptionFee = fee,
                SetupFee = 0m,
                TransactionFeePercent = plan.CustomerTransactionFeePercent,
                StartDate = now.Date,
                RenewalDate = renewal,
                Status = "Active"
            };
            await outletSubscriptions.AddAsync(subscription);
        }
        else
        {
            subscription.SaaSPlanId = plan.Id;
            subscription.BillingCycle = normalizedCycle;
            subscription.SubscriptionFee = fee;
            // Reactivation never silently charges the original outlet setup fee again.
            subscription.SetupFee = 0m;
            subscription.TransactionFeePercent = plan.CustomerTransactionFeePercent;
            subscription.StartDate = now.Date;
            subscription.RenewalDate = renewal;
            subscription.Status = "Active";
            await outletSubscriptions.UpdateAsync(subscription);
        }

        if (trial is not null && trial.Status == TrialStatus.Expired)
        {
            trial.Status = TrialStatus.Converted;
            trial.ConvertedAtUtc = now;
            await trials.UpdateAsync(trial);
        }

        outlet.Status = OutletStatus.Live;
        await outlets.UpdateAsync(outlet);

        return new AdminOutletReactivationDto(
            outlet.Id,
            subscription.Id,
            plan.Id,
            plan.Name,
            normalizedCycle,
            fee,
            subscription.SetupFee,
            subscription.StartDate,
            subscription.RenewalDate,
            subscription.Status,
            outlet.Status.ToString(),
            trial?.Status.ToString() ?? "",
            now,
            adminId,
            request.Reason.Trim());
    }
}
