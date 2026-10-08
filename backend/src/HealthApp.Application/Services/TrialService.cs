using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class TrialService(
    ICurrentUser current,
    IOutletSubscriptionRepository subscriptions,
    ISaaSPlanRepository plans,
    ITrialRepository trials) : ITrialService
{
    public async Task<TrialDto?> GetCurrentAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var trial = await trials.GetByOutletAsync(outletId);
        if (trial is null) return null;

        await ExpireIfNeededAsync(trial);
        return await MapAsync(trial);
    }

    public async Task<TrialDto?> StartAsync(StartTrialRequest request)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var plan = await plans.GetAsync(request.SaaSPlanId)
            ?? throw new KeyNotFoundException("SaaS plan not found.");

        if (!plan.IsActive)
            throw new InvalidOperationException("The selected SaaS plan is not active.");

        if (!string.Equals(request.BillingCycle, "Monthly", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.BillingCycle, "Annual", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Billing cycle must be Monthly or Annual.");

        var duration = Math.Clamp(request.DurationDays, 1, 90);
        var now = DateTime.UtcNow;
        var existingTrial = await trials.GetByOutletAsync(outletId);
        if (existingTrial is not null)
        {
            await ExpireIfNeededAsync(existingTrial);
            if (existingTrial.Status == TrialStatus.Active)
                throw new InvalidOperationException("This outlet already has an active trial.");
            if (existingTrial.Status == TrialStatus.Converted)
                throw new InvalidOperationException("This outlet has already converted a trial to a paid subscription.");
        }

        var subscription = await subscriptions.GetAnyByOutletAsync(outletId);
        if (subscription is not null && subscription.Status == "Active" && existingTrial is null)
            throw new InvalidOperationException("An active paid SaaS subscription already exists for this outlet.");

        if (subscription is null)
        {
            subscription = new OutletSubscription
            {
                Id = Guid.NewGuid(),
                OutletId = outletId
            };
        }

        subscription.SaaSPlanId = plan.Id;
        subscription.BillingCycle = request.BillingCycle;
        subscription.SubscriptionFee = 0m;
        subscription.SetupFee = 0m;
        subscription.TransactionFeePercent = plan.CustomerTransactionFeePercent;
        subscription.StartDate = now.Date;
        subscription.RenewalDate = now.Date.AddDays(duration);
        subscription.Status = "Active";

        if (await subscriptions.GetByOutletAsync(outletId) is not null)
            await subscriptions.UpdateAsync(subscription);
        else if (subscription.Id == Guid.Empty)
            throw new InvalidOperationException("Unable to create the trial subscription.");
        else
            await subscriptions.UpdateAsync(subscription);

        if (existingTrial is null)
        {
            existingTrial = new Trial
            {
                Id = Guid.NewGuid(),
                OutletId = outletId,
                OutletSubscriptionId = subscription.Id,
                StartedAtUtc = now,
                EndsAtUtc = now.AddDays(duration),
                Status = TrialStatus.Active,
                DurationDays = duration
            };
            await trials.AddAsync(existingTrial);
        }
        else
        {
            existingTrial.OutletSubscriptionId = subscription.Id;
            existingTrial.StartedAtUtc = now;
            existingTrial.EndsAtUtc = now.AddDays(duration);
            existingTrial.DurationDays = duration;
            existingTrial.Status = TrialStatus.Active;
            existingTrial.ConvertedAtUtc = null;
            existingTrial.CancelledAtUtc = null;
            existingTrial.CancellationReason = null;
            await trials.UpdateAsync(existingTrial);
        }

        return await MapAsync(existingTrial);
    }

    public async Task<TrialDto?> CancelAsync(string reason = "Cancelled by outlet administrator")
    {
        if (current.OutletId is not Guid outletId) return null;
        var trial = await trials.GetByOutletAsync(outletId);
        if (trial is null) return null;

        await ExpireIfNeededAsync(trial);
        if (trial.Status != TrialStatus.Active)
            return await MapAsync(trial);

        trial.Status = TrialStatus.Cancelled;
        trial.CancelledAtUtc = DateTime.UtcNow;
        trial.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by outlet administrator" : reason.Trim();
        await trials.UpdateAsync(trial);

        var subscription = await subscriptions.GetAnyByOutletAsync(outletId);
        if (subscription is not null && subscription.Id == trial.OutletSubscriptionId)
        {
            subscription.Status = "Cancelled";
            await subscriptions.UpdateAsync(subscription);
        }

        return await MapAsync(trial);
    }

    private async Task ExpireIfNeededAsync(Trial trial)
    {
        if (trial.Status == TrialStatus.Active && trial.EndsAtUtc <= DateTime.UtcNow)
        {
            trial.Status = TrialStatus.Expired;
            await trials.UpdateAsync(trial);

            var subscription = await subscriptions.GetAnyByOutletAsync(trial.OutletId);
            if (subscription is not null && subscription.Id == trial.OutletSubscriptionId && subscription.Status == "Active")
            {
                subscription.Status = "Expired";
                await subscriptions.UpdateAsync(subscription);
            }
        }
    }

    private async Task<TrialDto> MapAsync(Trial trial)
    {
        var plan = await plans.GetAsync((await subscriptions.GetAnyByOutletAsync(trial.OutletId))?.SaaSPlanId ?? Guid.Empty);
        var remaining = trial.Status == TrialStatus.Active
            ? Math.Max(0, (int)Math.Ceiling((trial.EndsAtUtc - DateTime.UtcNow).TotalDays))
            : 0;

        return new TrialDto(
            trial.Id,
            trial.OutletId,
            trial.OutletSubscriptionId,
            plan?.Id ?? Guid.Empty,
            plan?.Name ?? "SaaS Trial",
            (await subscriptions.GetAnyByOutletAsync(trial.OutletId))?.BillingCycle ?? "Monthly",
            trial.StartedAtUtc,
            trial.EndsAtUtc,
            trial.Status.ToString(),
            trial.DurationDays,
            remaining,
            trial.ConvertedAtUtc,
            trial.CancelledAtUtc,
            trial.CancellationReason ?? "");
    }
}
