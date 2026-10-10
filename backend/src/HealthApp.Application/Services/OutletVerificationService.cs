using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class OutletVerificationService(
    IOutletOnboardingRepository applications,
    ISaaSPlanRepository plans,
    IOutletRepository outlets,
    IOutletSubscriptionRepository outletSubscriptions,
    IUserRepository users,
    IFileStorage storage,
    ITransactionalEmailService emails,
    IOutletUrlService outletUrls,
    IConfiguration configuration) : IOutletVerificationService
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

    public async Task<ProtectedFileDownload?> GetDocumentAsync(Guid id, string documentType)
    {
        var x = await applications.GetAsync(id);
        if (x is null)
            return null;

        var type = NormalizeDocumentType(documentType)
            ?? throw new ArgumentException("Unsupported document type.");

        var key = type switch
        {
            "AadhaarCard" => x.AadhaarCardKey,
            "BusinessRegistration" => x.BusinessRegistrationKey,
            "BusinessPan" => x.BusinessPanDocumentKey,
            "GstCertificate" => x.GstCertificateKey,
            _ => ""
        };

        var fileName = type switch
        {
            "AadhaarCard" => x.AadhaarCardFileName,
            "BusinessRegistration" => x.BusinessRegistrationFileName,
            "BusinessPan" => x.BusinessPanDocumentFileName,
            "GstCertificate" => x.GstCertificateFileName,
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(key))
            return null;

        var file = await storage.OpenReadAsync(key);
        return file is null
            ? null
            : new ProtectedFileDownload(file.Content, file.ContentType, string.IsNullOrWhiteSpace(fileName) ? "document" : fileName);
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
            await emails.TrySendAsync(
                EmailTemplateId.OutletVerificationRejected,
                x.Email,
                new Dictionary<string, string?>
                {
                    ["OwnerName"] = x.OwnerName,
                    ["OutletName"] = x.OutletName,
                    ["Notes"] = string.IsNullOrWhiteSpace(x.VerificationNotes) ? "Please contact the Broccoly verification team for details." : x.VerificationNotes,
                    ["OutletAdminUrl"] = configuration["Email:OutletAdminUrl"] ?? "https://outlet.broccoly.in"
                });
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
            if (await users.FindByEmailAsync(x.Email) is not null)
                throw new InvalidOperationException("An account already exists for this email address.");
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
        var existingSubscription = await outletSubscriptions.GetByOutletAsync(outlet.Id);
        if (existingSubscription is null)
        {
            existingSubscription = new OutletSubscription
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
            await outletSubscriptions.AddAsync(existingSubscription);
        }
        else
        {
            existingSubscription.SaaSPlanId = plan.Id;
            existingSubscription.BillingCycle = x.BillingCycle;
            existingSubscription.SubscriptionFee = x.SubscriptionFee;
            existingSubscription.SetupFee = x.SetupFee;
            existingSubscription.TransactionFeePercent = plan.CustomerTransactionFeePercent;
            existingSubscription.Status = "Active";
            existingSubscription.RenewalDate = DateTime.UtcNow.Date.AddMonths(x.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase) ? 12 : 1);
            await outletSubscriptions.UpdateAsync(existingSubscription);
        }

        outlet.Status = OutletStatus.Active;
        outlet.BillingPlan = MapBillingPlan(plan.Name);
        await outlets.UpdateAsync(outlet);

        x.Status = "Approved";
        x.VerificationNotes = (request.Notes ?? "Approved by Broccoly verification team.").Trim();
        x.VerifiedAtUtc = DateTime.UtcNow;
        x.OutletId = outlet.Id;
        x.UserId = user.Id;
        await applications.UpdateAsync(x);

        await emails.TrySendAsync(
            EmailTemplateId.OutletVerificationApproved,
            x.Email,
            new Dictionary<string, string?>
            {
                ["OwnerName"] = x.OwnerName,
                ["OutletName"] = x.OutletName,
                ["PlanName"] = x.PlanName,
                ["StorefrontUrl"] = await outletUrls.GetStorefrontUrlAsync(outlet.Id),
                ["OutletAdminUrl"] = configuration["Email:OutletAdminUrl"] ?? "https://outlet.broccoly.in"
            });

        return ToDetail(x);
    }

    private static string? NormalizeDocumentType(string? value) =>
        value?.Trim().Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .ToLowerInvariant() switch
        {
            "aadhaar" or "aadhaarcard" => "AadhaarCard",
            "businessregistration" or "registration" => "BusinessRegistration",
            "businesspan" or "pan" => "BusinessPan",
            "gstcertificate" or "gst" => "GstCertificate",
            _ => null
        };

    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "outlet", "mail", "ftp", "smtp", "cdn", "static", "assets",
        "app", "auth", "login", "register", "support", "help", "status", "billing", "payments",
        "docs", "dev", "staging", "test", "demo"
    };

    private async Task<string> CreateUniqueSlugAsync(string name)
    {
        var baseSlug = Slugify(name);
        // Storefront subdomains are single DNS labels and must never collide with
        // platform endpoints or another outlet's hostname.
        baseSlug = baseSlug.Trim('-').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "outlet";
        if (ReservedSubdomains.Contains(baseSlug)) baseSlug = $"{baseSlug}-outlet";

        var slug = baseSlug;
        var counter = 2;
        while (ReservedSubdomains.Contains(slug) ||
               await outlets.GetBySlugAsync(slug) is not null ||
               await outlets.GetBySubdomainAsync(slug) is not null)
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
            x.AadhaarNumber, !string.IsNullOrWhiteSpace(x.AadhaarCardKey) ? $"/api/admin/outlet-onboarding/{x.Id}/documents/AadhaarCard" : "",
            !string.IsNullOrWhiteSpace(x.BusinessRegistrationKey) ? $"/api/admin/outlet-onboarding/{x.Id}/documents/BusinessRegistration" : "",
            x.BusinessPan, !string.IsNullOrWhiteSpace(x.BusinessPanDocumentKey) ? $"/api/admin/outlet-onboarding/{x.Id}/documents/BusinessPan" : "",
            x.GstNumber, !string.IsNullOrWhiteSpace(x.GstCertificateKey) ? $"/api/admin/outlet-onboarding/{x.Id}/documents/GstCertificate" : "",
            x.SubmittedAtUtc, x.VerificationNotes);
}
