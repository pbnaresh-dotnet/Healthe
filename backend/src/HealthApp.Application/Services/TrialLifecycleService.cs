using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class TrialLifecycleService(ICurrentUser current,IOutletSubscriptionRepository subscriptions,ISaaSPlanRepository plans,ITrialRepository trials):ITrialService
{
    public async Task<TrialDto?> GetCurrentAsync()
    {
        if(current.OutletId is not Guid outletId)return null;
        var trial=await trials.GetByOutletAsync(outletId);
        if(trial is null)return null;
        await ExpireIfNeededAsync(trial);
        return await MapAsync(trial);
    }

    public async Task<TrialDto?> StartAsync(StartTrialRequest request)
    {
        if(current.OutletId is not Guid outletId)throw new UnauthorizedAccessException("The current user is not associated with an outlet.");
        var plan=await plans.GetAsync(request.SaaSPlanId)??throw new KeyNotFoundException("SaaS plan not found.");
        if(!plan.IsActive)throw new InvalidOperationException("The selected SaaS plan is not active.");
        if(!string.Equals(request.BillingCycle,"Monthly",StringComparison.OrdinalIgnoreCase)&&!string.Equals(request.BillingCycle,"Annual",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Billing cycle must be Monthly or Annual.");
        var duration=Math.Clamp(request.DurationDays,1,90);
        var now=DateTime.UtcNow;
        var trial=await trials.GetByOutletAsync(outletId);
        if(trial is not null)
        {
            await ExpireIfNeededAsync(trial);
            if(trial.Status==TrialStatus.Active)throw new InvalidOperationException("This outlet already has an active trial.");
            if(trial.Status==TrialStatus.Converted)throw new InvalidOperationException("This outlet has already converted a trial to a paid subscription.");
        }
        var subscription=await subscriptions.GetAnyByOutletAsync(outletId);
        var existed=subscription is not null;
        if(subscription is not null&&subscription.Status=="Active"&&trial is null)
            throw new InvalidOperationException("An active paid SaaS subscription already exists for this outlet.");
        subscription??=new OutletSubscription{Id=Guid.NewGuid(),OutletId=outletId};
        subscription.SaaSPlanId=plan.Id;
        subscription.BillingCycle=request.BillingCycle;
        subscription.SubscriptionFee=0m;
        subscription.SetupFee=0m;
        subscription.TransactionFeePercent=plan.CustomerTransactionFeePercent;
        subscription.StartDate=now.Date;
        subscription.RenewalDate=now.Date.AddDays(duration);
        subscription.Status="Active";
        if(existed)await subscriptions.UpdateAsync(subscription);else await subscriptions.AddAsync(subscription);

        if(trial is null)
        {
            trial=new Trial{Id=Guid.NewGuid(),OutletId=outletId,OutletSubscriptionId=subscription.Id,StartedAtUtc=now,EndsAtUtc=now.AddDays(duration),Status=TrialStatus.Active,DurationDays=duration};
            await trials.AddAsync(trial);
        }
        else
        {
            trial.OutletSubscriptionId=subscription.Id;trial.StartedAtUtc=now;trial.EndsAtUtc=now.AddDays(duration);trial.DurationDays=duration;
            trial.Status=TrialStatus.Active;trial.ConvertedAtUtc=null;trial.CancelledAtUtc=null;trial.CancellationReason=null;
            await trials.UpdateAsync(trial);
        }
        return await MapAsync(trial);
    }

    public async Task<TrialDto?> CancelAsync(string reason="Cancelled by outlet administrator")
    {
        if(current.OutletId is not Guid outletId)return null;
        var trial=await trials.GetByOutletAsync(outletId);
        if(trial is null)return null;
        await ExpireIfNeededAsync(trial);
        if(trial.Status!=TrialStatus.Active)return await MapAsync(trial);
        trial.Status=TrialStatus.Cancelled;trial.CancelledAtUtc=DateTime.UtcNow;
        trial.CancellationReason=string.IsNullOrWhiteSpace(reason)?"Cancelled by outlet administrator":reason.Trim();
        await trials.UpdateAsync(trial);
        var subscription=await subscriptions.GetAnyByOutletAsync(outletId);
        if(subscription is not null&&subscription.Id==trial.OutletSubscriptionId){subscription.Status="Cancelled";await subscriptions.UpdateAsync(subscription);}
        return await MapAsync(trial);
    }

    private async Task ExpireIfNeededAsync(Trial trial)
    {
        if(trial.Status!=TrialStatus.Active||trial.EndsAtUtc>DateTime.UtcNow)return;
        trial.Status=TrialStatus.Expired;await trials.UpdateAsync(trial);
        var subscription=await subscriptions.GetAnyByOutletAsync(trial.OutletId);
        if(subscription is not null&&subscription.Id==trial.OutletSubscriptionId&&subscription.Status=="Active"){subscription.Status="Expired";await subscriptions.UpdateAsync(subscription);}
    }

    private async Task<TrialDto> MapAsync(Trial trial)
    {
        var subscription=await subscriptions.GetAnyByOutletAsync(trial.OutletId);
        var plan=subscription is null?null:await plans.GetAsync(subscription.SaaSPlanId);
        var remaining=trial.Status==TrialStatus.Active?Math.Max(0,(int)Math.Ceiling((trial.EndsAtUtc-DateTime.UtcNow).TotalDays)):0;
        return new TrialDto(trial.Id,trial.OutletId,trial.OutletSubscriptionId,plan?.Id??Guid.Empty,plan?.Name??"SaaS Trial",subscription?.BillingCycle??"Monthly",trial.StartedAtUtc,trial.EndsAtUtc,trial.Status.ToString(),trial.DurationDays,remaining,trial.ConvertedAtUtc,trial.CancelledAtUtc,trial.CancellationReason??"");
    }
}