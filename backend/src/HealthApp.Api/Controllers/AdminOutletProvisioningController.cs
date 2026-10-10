using System.Security.Claims;
using System.Text;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin")]
public sealed class AdminOutletProvisioningController(
    HealthAppDbContext db,
    IPasswordService passwords,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("saas-plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await db.SaaSPlans.AsNoTracking()
            .Where(x => x.IsActive && x.Name != "Free")
            .OrderBy(x => x.MonthlyFee)
            .Select(x => new
            {
                x.Id, x.Name, x.MonthlyFee, x.AnnualFee, x.IncludedActiveCustomers,
                x.AdditionalCustomerFee, x.CustomerTransactionFeePercent, x.Description,
                x.IsActive, SetupFee = configuration.GetValue<decimal?>("Onboarding:SetupFee") ?? 5000m
            })
            .ToListAsync(cancellationToken);
        return Ok(plans);
    }

    [HttpPost("outlets")]
    public async Task<IActionResult> CreateOutlet(CreateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var name = request.OutletName?.Trim() ?? "";
        var firstName = request.OwnerFirstName?.Trim() ?? "";
        var lastName = request.OwnerLastName?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var city = request.City?.Trim() ?? "";
        var state = request.State?.Trim() ?? "";
        var pincode = request.Pincode?.Trim() ?? "";
        var cycle = request.BillingCycle?.Trim() ?? "";

        if (name.Length is < 2 or > 160 || firstName.Length is < 1 or > 80 ||
            lastName.Length is < 1 or > 80 || !email.Contains('@') || email.Length > 254 ||
            city.Length is < 1 or > 100 || state.Length is < 1 or > 100 ||
            pincode.Length is < 3 or > 12 || string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 12)
            return BadRequest(new { message = "Provide valid outlet, owner, location and email details. The temporary password must be at least 12 characters." });

        if (cycle is not ("Monthly" or "SixMonths" or "Annual"))
            return BadRequest(new { message = "Billing cycle must be Monthly, SixMonths or Annual." });
        if (request.DiscountPercent < 0m || request.DiscountPercent > 100m ||
            decimal.Round(request.DiscountPercent, 2) != request.DiscountPercent)
            return BadRequest(new { message = "Discount must be between 0 and 100%, with at most two decimal places." });
        if (request.MarkAsPaid && !new[] { "Cash", "UPI", "BankTransfer", "Cashfree", "Other" }.Contains(request.PaymentMethod))
            return BadRequest(new { message = "Choose Cash, UPI, Bank transfer, Cashfree or Other as the payment method." });
        if (request.MarkAsPaid && string.IsNullOrWhiteSpace(request.PaymentReference) && request.PaymentMethod != "Cash")
            return BadRequest(new { message = "Enter a payment reference for non-cash payments." });

        if (await db.Users.AnyAsync(x => x.Email.ToLower() == email, cancellationToken))
            return Conflict(new { message = "An account already exists for this email address." });

        var plan = await db.SaaSPlans.SingleOrDefaultAsync(x => x.Id == request.SaaSPlanId && x.IsActive, cancellationToken);
        if (plan is null || string.Equals(plan.Name, "Free", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Select an active, paid SaaS subscription plan." });

        var monthlyFee = Math.Round(plan.MonthlyFee, 2, MidpointRounding.AwayFromZero);
        var subscriptionFee = cycle switch
        {
            "Annual" => Math.Round(plan.AnnualFee, 2, MidpointRounding.AwayFromZero),
            "SixMonths" => Math.Round(monthlyFee * 6m * 0.90m, 2, MidpointRounding.AwayFromZero),
            _ => monthlyFee
        };
        var setupFee = configuration.GetValue<decimal?>("Onboarding:SetupFee") ?? 5000m;
        if (setupFee < 0m || setupFee > 1000000m)
            return Problem("Configured outlet setup fee is outside the permitted range.");
        var undiscountedTotal = Math.Round(setupFee + subscriptionFee, 2, MidpointRounding.AwayFromZero);
        var discountAmount = Math.Round(undiscountedTotal * request.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var amountDue = Math.Max(0m, undiscountedTotal - discountAmount);

        var outletId = Guid.NewGuid();
        var slug = await CreateUniqueSlugAsync(name, cancellationToken);
        var now = DateTime.UtcNow;
        var outlet = new Outlet
        {
            Id = outletId,
            Name = name,
            Slug = slug,
            Subdomain = slug,
            City = city,
            State = state,
            Pincode = pincode,
            Status = OutletStatus.Live,
            BillingPlan = MapBillingPlan(plan.Name),
            About = "",
            DeliveryDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday",
            RestaurantGstRate = 5m,
            RestaurantGstMode = GstMode.Exclusive,
            LegalVersion = "1.0",
            LegalEffectiveDateUtc = now,
            LegalPoliciesPublished = false
        };
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            FirstName = firstName,
            LastName = lastName,
            MobileNumber = string.IsNullOrWhiteSpace(request.MobileNumber) ? null : request.MobileNumber.Trim(),
            Role = UserRole.OutletAdmin,
            OutletId = outletId,
            IsActive = true
        };
        var subscription = new OutletSubscription
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            SaaSPlanId = plan.Id,
            BillingCycle = cycle,
            SubscriptionFee = subscriptionFee,
            SetupFee = Math.Round(setupFee, 2, MidpointRounding.AwayFromZero),
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = discountAmount,
            TransactionFeePercent = plan.CustomerTransactionFeePercent,
            StartDate = now,
            RenewalDate = cycle switch
            {
                "Annual" => now.AddYears(1),
                "SixMonths" => now.AddMonths(6),
                _ => now.AddMonths(1)
            },
            Status = "Active"
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Outlets.Add(outlet);
        db.Users.Add(owner);
        db.OutletSubscriptions.Add(subscription);
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            PaymentType = "SaaSOutletProvisioning",
            Provider = request.MarkAsPaid && request.PaymentMethod == "Cashfree" ? "Cashfree" : request.MarkAsPaid ? "Manual" : "NotCollected",
            ProviderPaymentId = request.MarkAsPaid ? (request.PaymentReference?.Trim() ?? "") : "",
            ProviderOrderId = "",
            PaymentSessionId = "",
            PaymentMethod = request.MarkAsPaid ? request.PaymentMethod : "",
            ProviderStatus = request.MarkAsPaid ? "Paid" : "NotCollected",
            IdempotencyKey = $"admin-outlet-provisioning:{outletId:N}",
            RequestFingerprint = $"{plan.Id:N}|{cycle}|{request.DiscountPercent:0.##}|{amountDue:0.00}|{request.MarkAsPaid}|{request.PaymentMethod}",
            ProcessingStatus = request.MarkAsPaid ? "Completed" : "NotStarted",
            Amount = amountDue,
            Currency = "INR",
            Status = request.MarkAsPaid ? "Paid" : "Pending",
            GatewayResponseJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Source = "SuperAdminOutletProvisioning",
                CreatedByUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value,
                PlanName = plan.Name,
                BillingCycle = cycle,
                SetupFee = setupFee,
                SubscriptionFee = subscriptionFee,
                GrossAmount = undiscountedTotal,
                DiscountPercent = request.DiscountPercent,
                DiscountAmount = discountAmount,
                NetAmount = amountDue,
                PaymentMethod = request.MarkAsPaid ? request.PaymentMethod : null,
                PaymentReference = string.IsNullOrWhiteSpace(request.PaymentReference) ? null : request.PaymentReference.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.PaymentNotes) ? null : request.PaymentNotes.Trim()
            }),
            CreatedAtUtc = now,
            PaidAtUtc = request.MarkAsPaid ? now : null
        };
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var creator = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Created($"/api/admin/outlets/{outlet.Id}", new
        {
            outlet.Id,
            outlet.Name,
            outlet.Slug,
            outlet.Subdomain,
            outlet.City,
            outlet.State,
            Status = outlet.Status.ToString(),
            BillingPlan = outlet.BillingPlan.ToString(),
            OwnerId = owner.Id,
            OwnerEmail = owner.Email,
            Subscription = new
            {
                subscription.Id,
                subscription.SaaSPlanId,
                PlanName = plan.Name,
                subscription.BillingCycle,
                subscription.SubscriptionFee,
                subscription.SetupFee,
                subscription.DiscountPercent,
                subscription.DiscountAmount,
                GrossAmount = undiscountedTotal,
                AmountDue = amountDue,
                PaymentTransactionId = payment.Id,
                PaymentMethod = payment.PaymentMethod,
                PaymentStatus = payment.Status,
                subscription.TransactionFeePercent,
                subscription.StartDate,
                subscription.RenewalDate,
                subscription.Status,
                PaymentStatus = "NotCollected"
            },
            CreatedBy = creator
        });
    }

    private async Task<string> CreateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        var baseSlug = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "outlet";
        baseSlug = baseSlug.Length > 48 ? baseSlug[..48].TrimEnd('-') : baseSlug;
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "admin", "api", "outlet", "www", "login", "billing", "app", "demo" };
        if (reserved.Contains(baseSlug)) baseSlug = $"{baseSlug}-outlet";

        var slug = baseSlug;
        var suffix = 2;
        while (await db.Outlets.AnyAsync(x => x.Slug == slug || x.Subdomain == slug, cancellationToken))
        {
            var tail = $"-{suffix++}";
            slug = $"{baseSlug[..Math.Min(baseSlug.Length, 48 - tail.Length)]}{tail}";
        }
        return slug;
    }

    private static BillingPlan MapBillingPlan(string planName)
    {
        if (planName.Contains("scale", StringComparison.OrdinalIgnoreCase)) return BillingPlan.Scale;
        if (planName.Contains("growth", StringComparison.OrdinalIgnoreCase)) return BillingPlan.Growth;
        return BillingPlan.Starter;
    }
}

public sealed record CreateAdminOutletRequest(
    string OutletName,
    string OwnerFirstName,
    string OwnerLastName,
    string Email,
    string? MobileNumber,
    string Password,
    string City,
    string State,
    string Pincode,
    Guid SaaSPlanId,
    string BillingCycle,
    decimal DiscountPercent = 0m,
    bool MarkAsPaid = false,
    string PaymentMethod = "",
    string? PaymentReference = null,
    string? PaymentNotes = null);
