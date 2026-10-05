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
    IOutletRepository outlets,
    IOutletSubscriptionRepository outletSubscriptions,
    IUserRepository users,
    ICurrentUser current,
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
        var email = NormalizeEmail(request.Email);
        ValidateEmail(email);
        ValidatePassword(request.Password);
        var cycle = NormalizeCycle(request.BillingCycle);
        var plan = await plans.GetAsync(request.SaaSPlanId)
            ?? throw new KeyNotFoundException("Subscription plan not found.");
        if (!plan.IsActive) throw new InvalidOperationException("This subscription plan is no longer available.");
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("An account already exists for this email address. Please sign in instead.");

        ValidateInitialBusinessDetails(request);

        var id = Guid.NewGuid();
        var accessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var ownerParts = SplitName(request.OwnerName);
        var outlet = new Outlet
        {
            Id = Guid.NewGuid(),
            Name = request.OutletName.Trim(),
            Slug = await CreateUniqueSlugAsync(request.OutletName),
            Subdomain = "",
            City = request.City.Trim(),
            State = request.State.Trim(),
            Pincode = request.Pincode.Trim(),
            Status = OutletStatus.Pending,
            BillingPlan = MapBillingPlan(plan.Name),
            About = "",
            RestaurantGstRate = 5m,
            RestaurantGstMode = GstMode.Exclusive
        };
        outlet.Subdomain = outlet.Slug;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = ownerParts.FirstName,
            LastName = ownerParts.LastName,
            Role = UserRole.OutletAdmin,
            OutletId = outlet.Id,
            PasswordHash = passwords.Hash(request.Password),
            IsActive = true
        };

        var application = new OutletOnboardingApplication
        {
            Id = id,
            AccessKeyHash = Hash(accessKey),
            Email = email,
            PasswordHash = user.PasswordHash,
            AccountFirstName = user.FirstName,
            AccountLastName = user.LastName,
            SaaSPlanId = plan.Id,
            PlanName = plan.Name,
            BillingCycle = cycle,
            SubscriptionFee = cycle == "Annual" ? plan.AnnualFee : plan.MonthlyFee,
            SetupFee = SetupFee,
            PaymentStatus = "Paid",
            PaymentReference = $"ONB-{Guid.NewGuid():N}",
            Status = "PendingVerification",
            BusinessType = string.IsNullOrWhiteSpace(request.BusinessType) ? "Individual" : request.BusinessType.Trim(),
            OutletName = request.OutletName.Trim(),
            Description = "",
            City = request.City.Trim(),
            State = request.State.Trim(),
            Pincode = request.Pincode.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = request.AddressLine2?.Trim() ?? "",
            OwnerName = request.OwnerName.Trim(),
            OwnerEmail = email,
            OwnerPhone = request.OwnerPhone.Trim(),
            CreatedAtUtc = now,
            OutletId = outlet.Id,
            UserId = user.Id
        };

        var subscription = new OutletSubscription
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            SaaSPlanId = plan.Id,
            BillingCycle = cycle,
            SubscriptionFee = application.SubscriptionFee,
            SetupFee = SetupFee,
            TransactionFeePercent = plan.CustomerTransactionFeePercent,
            StartDate = now.Date,
            RenewalDate = now.Date.AddMonths(cycle == "Annual" ? 12 : 1),
            Status = "Pending"
        };

        await outlets.AddAsync(outlet);
        await users.AddAsync(user);
        await outletSubscriptions.AddAsync(subscription);
        await applications.AddAsync(application);

        return ToSession(application, accessKey);
    }

    public async Task<OutletOnboardingDto?> GetCurrentAsync()
    {
        if (current.UserId is not Guid userId) return null;
        var x = await applications.GetByUserIdAsync(userId);
        return x is null ? null : ToDto(x);
    }

    public async Task<OutletOnboardingDto?> SaveCurrentDetailsAsync(SaveOutletOnboardingDetailsRequest request)
    {
        var x = await CurrentApplicationAsync();
        if (x is null) return null;
        EnsureEditable(x);
        SaveDetails(x, request);
        await applications.UpdateAsync(x);
        return ToDto(x);
    }

    public async Task<OutletOnboardingDocumentDto?> UploadCurrentDocumentAsync(
        string documentType, Stream content, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var x = await CurrentApplicationAsync();
        if (x is null) return null;
        EnsureEditable(x);
        return await UploadDocumentInternalAsync(x, documentType, content, fileName, contentType, cancellationToken);
    }

    public async Task<OutletOnboardingDto?> SubmitCurrentAsync()
    {
        var x = await CurrentApplicationAsync();
        if (x is null) return null;
        EnsureEditable(x);
        ValidateSubmission(x);
        x.Status = "UnderVerification";
        x.SubmittedAtUtc = DateTime.UtcNow;
        x.VerifiedAtUtc = null;
        x.VerificationNotes = "";
        await applications.UpdateAsync(x);
        return ToDto(x);
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
        SaveDetails(x, request);
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
        return await UploadDocumentInternalAsync(x, documentType, content, fileName, contentType, cancellationToken);
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

    private async Task<OutletOnboardingApplication?> CurrentApplicationAsync() =>
        current.UserId is not Guid userId ? null : await applications.GetByUserIdAsync(userId);

    private async Task<OutletOnboardingDocumentDto> UploadDocumentInternalAsync(
        OutletOnboardingApplication x, string documentType, Stream content, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        var type = NormalizeDocumentType(documentType)
            ?? throw new ArgumentException("Unsupported document type.");
        var ext = Path.GetExtension(fileName);
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Supported document formats are JPG, PNG, WEBP and PDF.");
        var stored = await storage.UploadAsync(content, $"{x.Id:N}_{type}_{Guid.NewGuid():N}{ext}", contentType, "outlet-onboarding", cancellationToken);
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

    private static void SaveDetails(OutletOnboardingApplication x, SaveOutletOnboardingDetailsRequest request)
    {
        x.BusinessType = string.IsNullOrWhiteSpace(request.BusinessType) ? "Individual" : request.BusinessType.Trim();
        x.OutletName = request.OutletName.Trim();
        x.Description = request.Description?.Trim() ?? "";
        x.City = request.City.Trim();
        x.State = request.State.Trim();
        x.Pincode = request.Pincode.Trim();
        x.AddressLine1 = request.AddressLine1.Trim();
        x.AddressLine2 = request.AddressLine2?.Trim() ?? "";
        x.OwnerName = request.OwnerName.Trim();
        x.OwnerEmail = NormalizeEmail(request.OwnerEmail);
        x.OwnerPhone = request.OwnerPhone.Trim();
        x.AadhaarNumber = request.AadhaarNumber.Trim();
        x.BusinessPan = request.BusinessPan?.Trim().ToUpperInvariant() ?? "";
        x.GstNumber = request.GstNumber?.Trim().ToUpperInvariant() ?? "";
    }

    private static void EnsureEditable(OutletOnboardingApplication x)
    {
        if (x.PaymentStatus != "Paid")
            throw new InvalidOperationException("The onboarding payment must be completed first.");
        if (x.Status is "Approved")
            throw new InvalidOperationException("This outlet account has already been verified.");
        if (x.Status == "UnderVerification")
            throw new InvalidOperationException("This application is already under verification.");
        // Rejected applications are editable so the owner can correct details/documents and resubmit.
    }

    private static void ValidateInitialBusinessDetails(OutletOnboardingPaymentRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.OutletName)) throw new ArgumentException("Outlet name is required.");
        if (string.IsNullOrWhiteSpace(r.City) || string.IsNullOrWhiteSpace(r.State) || string.IsNullOrWhiteSpace(r.Pincode))
            throw new ArgumentException("City, state and pincode are required.");
        if (string.IsNullOrWhiteSpace(r.AddressLine1)) throw new ArgumentException("Business address is required.");
        if (string.IsNullOrWhiteSpace(r.OwnerName)) throw new ArgumentException("Owner name is required.");
        if (string.IsNullOrWhiteSpace(r.OwnerPhone)) throw new ArgumentException("Contact number is required.");
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


    private static string NormalizeEmail(string? value) => (value ?? "").Trim().ToLowerInvariant();

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Enter a valid email address.");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");
    }

    private static string NormalizeCycle(string? value) =>
        string.Equals(value, "Annual", StringComparison.OrdinalIgnoreCase) ? "Annual" : "Monthly";

    private static BillingPlan MapBillingPlan(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            "professional" or "scale" => BillingPlan.Scale,
            "growth" => BillingPlan.Growth,
            _ => BillingPlan.Starter
        };

    private async Task<string> CreateUniqueSlugAsync(string name)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var counter = 2;
        while (await outlets.GetBySlugAsync(slug) is not null)
            slug = $"{baseSlug}-{counter++}";
        return slug;
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        return string.IsNullOrWhiteSpace(slug)
            ? $"outlet-{Guid.NewGuid().ToString("N")[..8]}"
            : slug[..Math.Min(slug.Length, 90)];
    }

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var value = name.Trim();
        var index = value.IndexOf(' ');
        return index < 0 ? (value, "") : (value[..index], value[(index + 1)..].Trim());
    }

    private async Task<OutletOnboardingApplication?> AuthorizeAsync(Guid id, string accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey)) return null;
        var x = await applications.GetAsync(id);
        return x is not null && Hash(accessKey).Equals(x.AccessKeyHash, StringComparison.Ordinal) ? x : null;
    }

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
            x.SubmittedAtUtc, x.VerifiedAtUtc, x.VerificationNotes, x.UserId, x.OutletId);
    }
}
