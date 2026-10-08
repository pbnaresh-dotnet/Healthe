using HealthApp.Domain.Enums;

namespace HealthApp.Domain.Entities;

public sealed class OutletTaxProfile
{
    public Guid Id { get; set; }
    public Guid OutletId { get; set; }
    public string LegalName { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "India";
    public string Gstin { get; set; } = "";
    public string Pan { get; set; } = "";
    public bool IsGstRegistered { get; set; }
    public bool IsComposition { get; set; }
    public decimal RestaurantGstRate { get; set; } = 5m;
    public GstMode RestaurantGstMode { get; set; } = GstMode.Exclusive;
    public TaxOperatingMode TaxOperatingMode { get; set; } = TaxOperatingMode.DirectOutletSupplier;
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlatformTaxProfile
{
    public Guid Id { get; set; }
    public string LegalName { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "India";
    public string Gstin { get; set; } = "";
    public string Pan { get; set; } = "";
    public bool IsGstRegistered { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FinanceTaxRule
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public FinanceSupplyType SupplyType { get; set; }
    public TaxOperatingMode? TaxOperatingMode { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal CgstRatePercent { get; set; }
    public decimal SgstRatePercent { get; set; }
    public decimal IgstRatePercent { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public string Description { get; set; } = "";
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PaymentMerchantAccount
{
    public Guid Id { get; set; }
    public FinanceMerchantOwnerType OwnerType { get; set; }
    public Guid? OutletId { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderMerchantId { get; set; } = "";
    public string SettlementAccountReference { get; set; } = "";
    public string Currency { get; set; } = "INR";
    public string KycStatus { get; set; } = "Pending";
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FinancialDocument
{
    public Guid Id { get; set; }
    public Guid? OutletId { get; set; }
    public FinanceDocumentType DocumentType { get; set; }
    public FinanceSupplyType SupplyType { get; set; }
    public FinanceDocumentStatus Status { get; set; } = FinanceDocumentStatus.Draft;
    public FinancePartyType IssuerType { get; set; }
    public Guid IssuerId { get; set; }
    public FinancePartyType RecipientType { get; set; }
    public Guid? RecipientId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid? SourceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int FiscalYearStart { get; set; }
    public DateTime IssueDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime SupplyDateUtc { get; set; } = DateTime.UtcNow;
    public string Currency { get; set; } = "INR";
    public string PlaceOfSupplyState { get; set; } = "";
    public string PlaceOfSupplyStateCode { get; set; } = "";
    public bool ReverseCharge { get; set; }
    public bool IsEcoSection9_5 { get; set; }
    public Guid? OriginalDocumentId { get; set; }

    // Immutable issuer snapshot.
    public string SupplierLegalName { get; set; } = "";
    public string SupplierTradeName { get; set; } = "";
    public string SupplierAddressLine1 { get; set; } = "";
    public string SupplierAddressLine2 { get; set; } = "";
    public string SupplierCity { get; set; } = "";
    public string SupplierState { get; set; } = "";
    public string SupplierStateCode { get; set; } = "";
    public string SupplierPostalCode { get; set; } = "";
    public string SupplierGstin { get; set; } = "";
    public string SupplierPan { get; set; } = "";

    // Immutable recipient snapshot.
    public string RecipientName { get; set; } = "";
    public string RecipientAddressLine1 { get; set; } = "";
    public string RecipientAddressLine2 { get; set; } = "";
    public string RecipientCity { get; set; } = "";
    public string RecipientState { get; set; } = "";
    public string RecipientStateCode { get; set; } = "";
    public string RecipientPostalCode { get; set; } = "";
    public string RecipientGstin { get; set; } = "";
    public string RecipientPan { get; set; } = "";

    public decimal TaxableValue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal RoundedOffAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Notes { get; set; } = "";
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FinancialDocumentLine
{
    public Guid Id { get; set; }
    public Guid FinancialDocumentId { get; set; }
    public int LineNumber { get; set; }
    public string ItemType { get; set; } = "";
    public string Description { get; set; } = "";
    public string SacCode { get; set; } = "";
    public decimal Quantity { get; set; } = 1m;
    public string Unit { get; set; } = "unit";
    public decimal UnitPrice { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsTaxInclusive { get; set; }
}

public sealed class FinancialTaxComponent
{
    public Guid Id { get; set; }
    public Guid FinancialDocumentId { get; set; }
    public Guid? FinancialDocumentLineId { get; set; }
    public FinancialTaxComponentType Component { get; set; }
    public FinancialTaxNature Nature { get; set; } = FinancialTaxNature.Output;
    public decimal RatePercent { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Jurisdiction { get; set; } = "";
    public bool IsSection9_5Liability { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid FinancialDocumentId { get; set; }
    public Guid PaymentMerchantAccountId { get; set; }
    public string AllocationPurpose { get; set; } = "";
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reference { get; set; } = "";
}

public sealed class GatewayFee
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid PaymentMerchantAccountId { get; set; }
    public string ProviderFeeReference { get; set; } = "";
    public decimal FeeAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal GstRatePercent { get; set; }
    public DateTime ChargedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Settlement
{
    public Guid Id { get; set; }
    public Guid PaymentMerchantAccountId { get; set; }
    public Guid? OutletId { get; set; }
    public string ProviderSettlementId { get; set; } = "";
    public DateTime SettlementDateUtc { get; set; }
    public DateTime? BankValueDateUtc { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal GatewayFeeAmount { get; set; }
    public decimal GatewayFeeGstAmount { get; set; }
    public decimal PlatformDeductionAmount { get; set; }
    public decimal PlatformTaxAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal TaxDeductionAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Pending";
    public string BankReference { get; set; } = "";
    public string StatementReference { get; set; } = "";
    public DateTime? ReconciledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SettlementLine
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public Guid? FinancialDocumentId { get; set; }
    public FinanceSettlementLineType LineType { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
}

public sealed class RefundTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid? OutletId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid OriginalDocumentId { get; set; }
    public Guid? CreditNoteDocumentId { get; set; }
    public string ProviderRefundId { get; set; } = "";
    public RefundStatus Status { get; set; } = RefundStatus.Requested;
    public decimal RequestedAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal RestaurantGstRefund { get; set; }
    public decimal PlatformFeeRefund { get; set; }
    public decimal PlatformGstRefund { get; set; }
    public decimal GatewayRefundFee { get; set; }
    public string Reason { get; set; } = "";
    public string ApprovalReference { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
}

public sealed class LedgerAccount
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = "";
    public string Name { get; set; } = "";
    public LedgerAccountType AccountType { get; set; }
    public LedgerNormalBalance NormalBalance { get; set; }
    public Guid? OutletId { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LedgerJournal
{
    public Guid Id { get; set; }
    public Guid? OutletId { get; set; }
    public string JournalNumber { get; set; } = "";
    public string SourceType { get; set; } = "";
    public Guid? SourceId { get; set; }
    public DateTime JournalDateUtc { get; set; } = DateTime.UtcNow;
    public string Memo { get; set; } = "";
    public LedgerJournalStatus Status { get; set; } = LedgerJournalStatus.Draft;
    public DateTime? PostedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LedgerJournalLine
{
    public Guid Id { get; set; }
    public Guid LedgerJournalId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public Guid? FinancialTaxComponentId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Reference { get; set; } = "";
}

public sealed class DocumentNumberSequence
{
    public Guid Id { get; set; }
    public FinancePartyType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public FinanceDocumentType DocumentType { get; set; }
    public int FiscalYearStart { get; set; }
    public string Prefix { get; set; } = "";
    public long LastNumber { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}


public sealed class FinancePolicyDocument
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FinancePolicyDocumentVersion
{
    public Guid Id { get; set; }
    public Guid FinancePolicyDocumentId { get; set; }
    public string Version { get; set; } = "";
    public FinancePolicyPublicationStatus Status { get; set; } = FinancePolicyPublicationStatus.Draft;
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public string ChangeSummary { get; set; } = "";
    public string ChangeReason { get; set; } = "";
    public string SourceCodeReference { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public Guid? PreviousVersionId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class FinancePolicyDocumentSection
{
    public Guid Id { get; set; }
    public Guid FinancePolicyDocumentVersionId { get; set; }
    public string SectionCode { get; set; } = "";
    public string Title { get; set; } = "";
    public int DisplayOrder { get; set; }
    public string ContentMarkdown { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}


public sealed class FinanceCalculationSnapshot
{
    public Guid Id { get; set; }
    public Guid OutletId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? FinancePolicyDocumentVersionId { get; set; }
    public Guid TaxProfileId { get; set; }
    public Guid RestaurantTaxRuleId { get; set; }
    public Guid PlatformTaxRuleId { get; set; }
    public Guid CommissionTaxRuleId { get; set; }
    public bool RestaurantTaxApplicable { get; set; }
    public TaxOperatingMode RestaurantTaxOperatingMode { get; set; }
    public GstMode RestaurantGstMode { get; set; }
    public decimal RestaurantRate { get; set; }
    public decimal RestaurantTaxableAmount { get; set; }
    public decimal RestaurantTaxAmount { get; set; }
    public decimal PlatformServiceFee { get; set; }
    public decimal PlatformTaxRate { get; set; }
    public decimal PlatformTaxAmount { get; set; }
    public decimal CommissionTaxRate { get; set; }
    public decimal CommissionTaxAmount { get; set; }
    public decimal CommissionBaseAmount { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public string InputHash { get; set; } = "";
    public string InputsJson { get; set; } = "";
    public string ResultsJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
