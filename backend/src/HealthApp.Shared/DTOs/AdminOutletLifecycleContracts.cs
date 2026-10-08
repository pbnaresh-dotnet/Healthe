namespace HealthApp.Shared.DTOs;

public record AdminOutletReactivationRequest(
    Guid SaaSPlanId,
    string BillingCycle = "Monthly",
    string Reason = "Super Admin reactivation");

public record AdminOutletReactivationOptionsDto(
    Guid OutletId,
    string OutletStatus,
    string SubscriptionStatus,
    string TrialStatus,
    Guid? CurrentSaaSPlanId,
    IReadOnlyList<SaaSPlanDto> AvailablePlans,
    bool CanReactivate);

public record AdminOutletReactivationDto(
    Guid OutletId,
    Guid OutletSubscriptionId,
    Guid SaaSPlanId,
    string PlanName,
    string BillingCycle,
    decimal SubscriptionFee,
    decimal SetupFee,
    DateTime StartDate,
    DateTime RenewalDate,
    string SubscriptionStatus,
    string OutletStatus,
    string TrialStatus,
    DateTime ReactivatedAtUtc,
    Guid? ReactivatedByUserId,
    string Reason);
