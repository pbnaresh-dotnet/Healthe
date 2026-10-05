using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class OutletVerificationService(
    IOutletOnboardingRepository applications,
    ISaaSPlanRepository plans,
    IOutletRepository outlets,
    IOutletSubscriptionRepository outletSubscriptions,
    IUserRepository users) : IOutletVerificationService
{
    public async Task<IReadOnlyList<OutletVerificationSummaryDto>> GetPendingAsync() =>
        (await applications.GetByStatusAsync("UnderVerification"))
            .Select(x => new OutletVerificationSummaryDto(
                x.Id, x.OutletName, x.OwnerName, x.Email, x.City, x.BusinessType,
                x.PlanName, x.Status, x.PaymentStatus, x.CreatedAtUtc, x.SubmittedAtUtc))
            .ToList();

    public async Task<OutletVerificationDetailDto?> GetAsync(Guid id)
    {
        var x = await applications.GetAsync(id);
        return x is null ? null : ToDetail(x);
    }

    public async Task<OutletVerificationDetailDto?> DecideAsync(Guid id, DecideOutletVerificationRequest request)
    {
        var x = await applications.GetAsync(id);
        if (x is null) return null;
        if (x.Status != "UnderVerification")
            throw new InvalidOperationException("Only applications under verification can be approved or rejected.");
        if (x.PaymentStatus != "Paid")
            throw new InvalidOperationException("The onboarding payment has not been completed.");

        if (!request.Approve)
        {
            x.Status = "Rejected";
            x.VerificationNotes = (request.Notes ?? "").Trim();
            x.VerifiedAtUtc = DateTime.UtcNow;
            await applications.UpdateAsync(x);
            return ToDetail(x);
        }

        if (await users.FindByEmailAsync(x.Email) is not null)
            throw new InvalidOperationException("An account already exists for this email address.");

        var slugBase = Slugify(x.OutletName);
        var slug = slugBase;
        var counter = 2;
        while (await outlets.GetBySlugAsync(slug) is not null)
            slug = $"{slugBase}-{counter++}";

        var plan = await plans.GetAsync(x.SaaSPlanId) ?? throw new KeyNotFoundException("Subscription plan not found.");
        var billingPlan = Enum.TryParse<BillingPlan>(plan.Name, true, out var bp) ? bp : BillingPlan.Growth;
        var outlet = new Outlet
        {
            Id = Guid.NewGuid(),
            Name = x.OutletName,
            Slug = slug,
            Subdomain = slug,
            City = x.City,
            State = x.State,
            Pincode = x.Pincode,
            Status = OutletStatus.Active,
            BillingPlan = billingPlan,
            About = x.Description,
            RestaurantGstRate = 5m,
            RestaurantGstMode = GstMode.Exclusive
        };
        await outlets.AddAsync(outlet);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = x.Email,
            FirstName = string.IsNullOrWhiteSpace(x.OwnerName) ? x.AccountFirstName : x.OwnerName.Split(' ', 2)[0],
            LastName = string.IsNullOrWhiteSpace(x.OwnerName) ? x.AccountLastName : (x.OwnerName.Contains(' ') ? x.OwnerName[(x.OwnerName.IndexOf(' ') + 1)..] : ""),
            Role = UserRole.OutletAdmin,
            OutletId = outlet.Id,
            PasswordHash = x.PasswordHash,
            IsActive = true
        };
        await users.AddAsync(user);

        var subscription = new OutletSubscription
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            SaaSPlanId = plan.Id,
            BillingCycle = x.BillingCycle,
            SubscriptionFee = x.SubscriptionFee,
            SetupFee = x.SetupFee,
            TransactionFeePercent = plan.CustomerTransactionFeePercent,
            StartDate = DateTime.UtcNow.Date,
            RenewalDate = DateTime.UtcNow.Date.AddMonths(x.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase) ? 12 : 1),
            Status = "Active"
        };
        await outletSubscriptions.AddAsync(subscription);

        x.Status = "Approved";
        x.VerificationNotes = (request.Notes ?? "Approved by HealthApp verification team.").Trim();
        x.VerifiedAtUtc = DateTime.UtcNow;
        x.OutletId = outlet.Id;
        x.UserId = user.Id;
        await applications.UpdateAsync(x);

        return ToDetail(x);
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        return string.IsNullOrWhiteSpace(slug) ? $"outlet-{Guid.NewGuid().ToString("N")[..8]}" : slug[..Math.Min(slug.Length, 90)];
    }

    private static OutletVerificationDetailDto ToDetail(OutletOnboardingApplication x) =>
        new(
            x.Id, x.Status, x.PaymentStatus, x.PlanName, x.BillingCycle, x.SubscriptionFee, x.SetupFee,
            x.BusinessType, x.OutletName, x.Description, x.City, x.State, x.Pincode,
            x.AddressLine1, x.AddressLine2, x.OwnerName, x.OwnerEmail, x.OwnerPhone,
            x.AadhaarNumber, x.AadhaarCardUrl, x.BusinessRegistrationUrl,
            x.BusinessPan, x.BusinessPanDocumentUrl, x.GstNumber, x.GstCertificateUrl,
            x.SubmittedAtUtc, x.VerificationNotes);
}
