namespace HealthApp.Domain.Entities;

public sealed class PaymentSettlementReconciliationException
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderPaymentId { get; set; } = "";
    public string ProviderSettlementId { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string Status { get; set; } = "Open";
    public decimal? ReportedGrossAmount { get; set; }
    public decimal? ReportedNetSettlementAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public string RawRowJson { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public string ResolutionNotes { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public string ResolvedBy { get; set; } = "";
}