namespace HealthApp.Domain.Entities;

public sealed class OutletLegalPolicyVersion
{
    public Guid Id { get; set; }
    public Guid OutletId { get; set; }
    public string Version { get; set; } = "1.0";
    public string CustomerTermsAndConditions { get; set; } = "";
    public string CustomerPrivacyPolicy { get; set; } = "";
    public string CancellationRefundPolicy { get; set; } = "";
    public string MealSkipReschedulePolicy { get; set; } = "";
    public string DeliveryPolicy { get; set; } = "";
    public string AllergenDietaryDisclaimer { get; set; } = "";
    public string PaymentPricingPromotionalTerms { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public DateTime EffectiveDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class CustomerLegalAcceptance
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OutletId { get; set; }
    public Guid LegalPolicyVersionId { get; set; }
    public bool TermsAccepted { get; set; }
    public bool PrivacyAccepted { get; set; }
    public bool CommercialPoliciesAccepted { get; set; }
    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}