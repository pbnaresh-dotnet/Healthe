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

        var plan = await plans.GetAsync(x.SaaSPlanId) ?? throw new KeyNotFoundException("Subscription plan not found.");
        var outlet = x.OutletId.HasValue ? await outlets.GetByIdAsync(x.OutletId.Value) : null;
        var user = x.UserId.HasValue ? await users.FindByIdAsync(x.UserId.Value) : null;

        if (outlet is null)
        {
            var slug = await CreateUniqueSlugAsync(x.OutletName);
            outlet = new Outlet
            {
                Id = Guid.NewGuid(),
                Name = x.OutletName,
                Slug = slug,
                Subdomain = slug,
                City = x.City,
                State = x.State,
                Pincode = x.Pincode,
                Status = OutletStatus.Pending,
                BillingPlan = MapBillingPlan(plan.Name),
                About = x.Description,
                RestaurantGstRate = 5m,
                RestaurantGstMode = GstMode.Exclusive
            };
            await outlets.AddAsync(outlet);
        }

        if (user is null)
        {
            var parts = SplitName(x.OwnerName);
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = x.Email,
                FirstName = parts.FirstName,
                LastName = parts.LastName,
                Role = UserRole.OutletAdmin,
                OutletId = outlet.Id,
                PasswordHash = x.PasswordHash,
                IsActive = true
            };
            await users.AddAsync(user);
        }
        else
        {
            user.OutletId = outlet.Id;
            user.IsActive = true;
            user.PasswordHash = x.PasswordHash;
            await users.UpdateAsync(user);
        }

        var existingSubscription = await outletSubscriptions.GetByOutletAsync(outlet.Id);
        if (existingSubscription is null)
        {
            await outletSubscriptions.AddAsync(new OutletSubscription
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
                Status = "Pending"
            });
        }

        outlet.Status = OutletStatus.Active;
        outlet.BillingPlan = MapBillingPlan(plan.Name);
        await outlets.UpdateAsync(outlet);

        x.Status = "Approved";
        x.VerificationNotes = (request.Notes ?? "Approved by HealthApp verification team.").Trim();
        x.VerifiedAtUtc = DateTime.UtcNow;
        x.OutletId = outlet.Id;
        x.UserId = user.Id;
        await applications.UpdateAsync(x);

        return ToDetail(x);
    }

    private async Task<string> CreateUniqueSlugAsync(string name)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var counter = 2;
        while (await outlets.GetBySlugAsync(slug) is not null)
            slug = $"{baseSlug}-{counter++}";
        return slug;
    }

    private static BillingPlan MapBillingPlan(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            "professional" or "scale" => BillingPlan.Scale,
            "growth" => BillingPlan.Growth,
            _ => BillingPlan.Starter
        };

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var value = name.Trim();
        var index = value.IndexOf(' ');
        return index < 0 ? (value, "") : (value[..index], value[(index + 1)..].Trim());
    }

    private static string Slugify(string value)
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
