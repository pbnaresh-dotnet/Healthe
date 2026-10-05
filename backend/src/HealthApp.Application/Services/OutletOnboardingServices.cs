using System.Security.Cryptography;
using System.Text;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class OutletOnboardingService(
    IOutletOnboardingRepository applications,
    ISaaSPlanRepository plans,
    IFileStorage storage,
    IPasswordService passwords) : IOutletOnboardingService
{
    private const decimal SetupFee = 5000m;

    public async Task<IReadOnlyList<SaaSPlanDto>> GetPlansAsync() =>
        (await plans.GetActiveAsync()).Select(x => new SaaSPlanDto(
            x.Id, x.Name, x.MonthlyFee, x.AnnualFee, x.IncludedActiveCustomers,
            x.AdditionalCustomerFee, x.CustomerTransactionFeePercent, x.Description, x.IsActive)).ToList();

    public async Task<OutletOnboardingSessionDto> StartPaymentAsync(OutletOnboardingPaymentRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Enter a valid email address.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");
        if (!string.Equals(request.BillingCycle, "Annual", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.BillingCycle, "Monthly", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Billing cycle must be Monthly or Annual.");

        var plan = await plans.GetAsync(request.SaaSPlanId)
            ?? throw new KeyNotFoundException("Subscription plan not found.");
        if (!plan.IsActive)
            throw new InvalidOperationException("This subscription plan is no longer available.");

        var id = Guid.NewGuid();
        var accessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var application = new OutletOnboardingApplication
        {
            Id = id,
            AccessKeyHash = Hash(accessKey),
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            SaaSPlanId = plan.Id,
            PlanName = plan.Name,
            BillingCycle = string.Equals(request.BillingCycle, "Annual", StringComparison.OrdinalIgnoreCase) ? "Annual" : "Monthly",
            SubscriptionFee = string.Equals(request.BillingCycle, "Annual", StringComparison.OrdinalIgnoreCase) ? plan.AnnualFee : plan.MonthlyFee,
            SetupFee = SetupFee,
            PaymentStatus = "Paid",
            PaymentReference = $"ONB-{Guid.NewGuid():N}",
            Status = "Onboarding",
            CreatedAtUtc = DateTime.UtcNow
        };
        await applications.AddAsync(application);
        return ToSession(application, accessKey);
    }

    public async Task<OutletOnboardingDto?> GetAsync(Guid id, string accessKey)
    {
        var x = await AuthorizeAsync(id, accessKey);
        return x is null ? null : ToDto(x);
    }

    public async Task<OutletOnboardingDto?> SaveDetailsAsync(Guid id, string accessKey, SaveOutletOnboardingDetailsRequest request)
    {
        var x = await AuthorizeAsync(id, accessKey);
        if (x is null) return null;
        EnsureEditable(x);

        x.BusinessType = string.IsNullOrWhiteSpace(request.BusinessType) ? "Individual" : request.BusinessType.Trim();
        x.OutletName = request.OutletName.Trim();
        x.Description = request.Description?.Trim() ?? "";
        x.City = request.City.Trim();
        x.State = request.State.Trim();
        x.Pincode = request.Pincode.Trim();
        x.AddressLine1 = request.AddressLine1.Trim();
        x.AddressLine2 = request.AddressLine2?.Trim() ?? "";
        x.OwnerName = request.OwnerName.Trim();
        x.OwnerEmail = (request.OwnerEmail ?? "").Trim().ToLowerInvariant();
        x.OwnerPhone = request.OwnerPhone.Trim();
        x.AadhaarNumber = request.AadhaarNumber.Trim();
        x.BusinessPan = request.BusinessPan?.Trim().ToUpperInvariant() ?? "";
        x.GstNumber = request.GstNumber?.Trim().ToUpperInvariant() ?? "";
        await applications.UpdateAsync(x);
        return ToDto(x);
    }

    public async Task<OutletOnboardingDocumentDto?> UploadDocumentAsync(
        Guid id, string accessKey, string documentType, Stream content, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var x = await AuthorizeAsync(id, accessKey);
        if (x is null) return null;
        EnsureEditable(x);

        var type = NormalizeDocumentType(documentType);
        if (type is null)
            throw new ArgumentException("Unsupported document type.");
        var ext = Path.GetExtension(fileName);
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Supported document formats are JPG, PNG, WEBP and PDF.");
        var stored = await storage.UploadAsync(content, $"{id:N}_{type}_{Guid.NewGuid():N}{ext}", contentType, "outlet-onboarding", cancellationToken);
        var now = DateTime.UtcNow;

        switch (type)
        {
            case "AadhaarCard":
                x.AadhaarCardUrl = stored.Url;
                x.AadhaarCardFileName = fileName;
                break;
            case "BusinessRegistration":
                x.BusinessRegistrationUrl = stored.Url;
                x.BusinessRegistrationFileName = fileName;
                break;
            case "BusinessPan":
                x.BusinessPanDocumentUrl = stored.Url;
                x.BusinessPanDocumentFileName = fileName;
                break;
            case "GstCertificate":
                x.GstCertificateUrl = stored.Url;
                x.GstCertificateFileName = fileName;
                break;
        }

        await applications.UpdateAsync(x);
        return new OutletOnboardingDocumentDto(type, stored.Url, fileName, now);
    }

    public async Task<OutletOnboardingDto?> SubmitAsync(Guid id, string accessKey)
    {
        var x = await AuthorizeAsync(id, accessKey);
        if (x is null) return null;
        EnsureEditable(x);

        ValidateSubmission(x);
        x.Status = "UnderVerification";
        x.SubmittedAtUtc = DateTime.UtcNow;
        x.VerificationNotes = "";
        await applications.UpdateAsync(x);
        return ToDto(x);
    }

    private async Task<OutletOnboardingApplication?> AuthorizeAsync(Guid id, string accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey)) return null;
        var x = await applications.GetAsync(id);
        return x is not null && Hash(accessKey).Equals(x.AccessKeyHash, StringComparison.Ordinal) ? x : null;
    }

    private static void EnsureEditable(OutletOnboardingApplication x)
    {
        if (x.PaymentStatus != "Paid")
            throw new InvalidOperationException("Payment must be completed before onboarding can start.");
        if (x.Status is "UnderVerification" or "Approved")
            throw new InvalidOperationException("This onboarding application has already been submitted.");
        if (x.Status == "Rejected")
            throw new InvalidOperationException("This application was rejected. Please contact HealthApp support.");
    }

    private static void ValidateSubmission(OutletOnboardingApplication x)
    {
        if (string.IsNullOrWhiteSpace(x.OutletName)) throw new ArgumentException("Outlet name is required.");
        if (string.IsNullOrWhiteSpace(x.City) || string.IsNullOrWhiteSpace(x.State) || string.IsNullOrWhiteSpace(x.Pincode))
            throw new ArgumentException("City, state and pincode are required.");
        if (string.IsNullOrWhiteSpace(x.AddressLine1)) throw new ArgumentException("Business address is required.");
        if (string.IsNullOrWhiteSpace(x.OwnerName) || string.IsNullOrWhiteSpace(x.OwnerPhone))
            throw new ArgumentException("Owner name and phone are required.");
        if (string.IsNullOrWhiteSpace(x.AadhaarNumber) || string.IsNullOrWhiteSpace(x.AadhaarCardUrl))
            throw new ArgumentException("Owner Aadhaar number and Aadhaar card are required.");

        if (IsRegisteredBusiness(x))
        {
            if (string.IsNullOrWhiteSpace(x.BusinessRegistrationUrl))
                throw new ArgumentException("Business registration or equivalent document is required.");
            if (string.IsNullOrWhiteSpace(x.BusinessPan) || string.IsNullOrWhiteSpace(x.BusinessPanDocumentUrl))
                throw new ArgumentException("Business PAN and PAN document are required.");
            if (string.IsNullOrWhiteSpace(x.GstNumber) || string.IsNullOrWhiteSpace(x.GstCertificateUrl))
                throw new ArgumentException("GST number and GST certificate are required for a registered business.");
        }
    }

    private static bool IsRegisteredBusiness(OutletOnboardingApplication x) =>
        x.BusinessType.Equals("RegisteredBusiness", StringComparison.OrdinalIgnoreCase) ||
        x.BusinessType.Equals("Registered Business", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeDocumentType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "aadhaar" or "aadhaarcard" => "AadhaarCard",
            "registration" or "businessregistration" or "companyregistration" => "BusinessRegistration",
            "pan" or "businesspan" => "BusinessPan",
            "gst" or "gstcertificate" => "GstCertificate",
            _ => null
        };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static OutletOnboardingSessionDto ToSession(OutletOnboardingApplication x, string accessKey) =>
        new(x.Id, accessKey, x.Status, x.PaymentStatus, x.PlanName, x.BillingCycle, x.SubscriptionFee, x.SetupFee, x.SubmittedAtUtc);

    private static OutletOnboardingDto ToDto(OutletOnboardingApplication x)
    {
        var documents = new List<OutletOnboardingDocumentDto>();
        if (!string.IsNullOrWhiteSpace(x.AadhaarCardUrl)) documents.Add(new("AadhaarCard", x.AadhaarCardUrl, x.AadhaarCardFileName, x.CreatedAtUtc));
        if (!string.IsNullOrWhiteSpace(x.BusinessRegistrationUrl)) documents.Add(new("BusinessRegistration", x.BusinessRegistrationUrl, x.BusinessRegistrationFileName, x.CreatedAtUtc));
        if (!string.IsNullOrWhiteSpace(x.BusinessPanDocumentUrl)) documents.Add(new("BusinessPan", x.BusinessPanDocumentUrl, x.BusinessPanDocumentFileName, x.CreatedAtUtc));
        if (!string.IsNullOrWhiteSpace(x.GstCertificateUrl)) documents.Add(new("GstCertificate", x.GstCertificateUrl, x.GstCertificateFileName, x.CreatedAtUtc));

        return new(
            x.Id, x.Status, x.PaymentStatus, x.PlanName, x.BillingCycle, x.SubscriptionFee, x.SetupFee,
            x.BusinessType, x.OutletName, x.Description, x.City, x.State, x.Pincode,
            x.AddressLine1, x.AddressLine2, x.OwnerName, x.OwnerEmail, x.OwnerPhone,
            x.AadhaarNumber, x.BusinessPan, x.GstNumber, documents,
            x.SubmittedAtUtc, x.VerifiedAtUtc, x.VerificationNotes);
    }
}
