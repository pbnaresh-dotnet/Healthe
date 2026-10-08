using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthApp.Infrastructure.Data;

internal static class FinanceModelBuilder
{
    public static void Configure(ModelBuilder b)
    {
        ConfigureOutletTaxProfile(b.Entity<OutletTaxProfile>());
        ConfigurePlatformTaxProfile(b.Entity<PlatformTaxProfile>());
        ConfigureTaxRule(b.Entity<FinanceTaxRule>());
        ConfigureMerchantAccount(b.Entity<PaymentMerchantAccount>());
        ConfigureFinancialDocument(b.Entity<FinancialDocument>());
        ConfigureFinancialDocumentLine(b.Entity<FinancialDocumentLine>());
        ConfigureFinancialTaxComponent(b.Entity<FinancialTaxComponent>());
        ConfigurePaymentAllocation(b.Entity<PaymentAllocation>());
        ConfigureGatewayFee(b.Entity<GatewayFee>());
        ConfigureSettlement(b.Entity<Settlement>());
        ConfigureSettlementLine(b.Entity<SettlementLine>());
        ConfigureRefundTransaction(b.Entity<RefundTransaction>());
        ConfigureLedgerAccount(b.Entity<LedgerAccount>());
        ConfigureLedgerJournal(b.Entity<LedgerJournal>());
        ConfigureLedgerJournalLine(b.Entity<LedgerJournalLine>());
        ConfigureDocumentNumberSequence(b.Entity<DocumentNumberSequence>());
    }

    private static void ConfigureOutletTaxProfile(EntityTypeBuilder<OutletTaxProfile> e)
    {
        e.ToTable("OutletTaxProfiles");
        e.HasKey(x => x.Id);
        e.Property(x => x.LegalName).HasMaxLength(250).IsRequired();
        e.Property(x => x.TradeName).HasMaxLength(250).IsRequired();
        e.Property(x => x.AddressLine1).HasMaxLength(500).IsRequired();
        e.Property(x => x.AddressLine2).HasMaxLength(500).IsRequired();
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.StateCode).HasMaxLength(10).IsRequired();
        e.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.Country).HasMaxLength(100).IsRequired();
        e.Property(x => x.Gstin).HasMaxLength(20).IsRequired();
        e.Property(x => x.Pan).HasMaxLength(20).IsRequired();
        e.Property(x => x.RestaurantGstRate).HasPrecision(9,4);
        e.Property(x => x.RestaurantGstMode).HasConversion<int>();
        e.Property(x => x.TaxOperatingMode).HasConversion<int>();
        e.HasIndex(x => new { x.OutletId, x.EffectiveFromUtc }).IsUnique();
        e.HasIndex(x => new { x.OutletId, x.IsActive });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePlatformTaxProfile(EntityTypeBuilder<PlatformTaxProfile> e)
    {
        e.ToTable("PlatformTaxProfiles");
        e.HasKey(x => x.Id);
        e.Property(x => x.LegalName).HasMaxLength(250).IsRequired();
        e.Property(x => x.TradeName).HasMaxLength(250).IsRequired();
        e.Property(x => x.AddressLine1).HasMaxLength(500).IsRequired();
        e.Property(x => x.AddressLine2).HasMaxLength(500).IsRequired();
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.StateCode).HasMaxLength(10).IsRequired();
        e.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.Country).HasMaxLength(100).IsRequired();
        e.Property(x => x.Gstin).HasMaxLength(20).IsRequired();
        e.Property(x => x.Pan).HasMaxLength(20).IsRequired();
        e.HasIndex(x => new { x.EffectiveFromUtc }).IsUnique();
        e.HasIndex(x => x.IsActive);
    }

    private static void ConfigureTaxRule(EntityTypeBuilder<FinanceTaxRule> e)
    {
        e.ToTable("FinanceTaxRules");
        e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(120).IsRequired();
        e.Property(x => x.SupplyType).HasConversion<int>();
        e.Property(x => x.TaxOperatingMode).HasConversion<int>();
        e.Property(x => x.TaxRatePercent).HasPrecision(9,4);
        e.Property(x => x.CgstRatePercent).HasPrecision(9,4);
        e.Property(x => x.SgstRatePercent).HasPrecision(9,4);
        e.Property(x => x.IgstRatePercent).HasPrecision(9,4);
        e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.SupplyType, x.TaxOperatingMode, x.EffectiveFromUtc });
    }

    private static void ConfigureMerchantAccount(EntityTypeBuilder<PaymentMerchantAccount> e)
    {
        e.ToTable("PaymentMerchantAccounts");
        e.HasKey(x => x.Id);
        e.Property(x => x.OwnerType).HasConversion<int>();
        e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        e.Property(x => x.ProviderMerchantId).HasMaxLength(200).IsRequired();
        e.Property(x => x.SettlementAccountReference).HasMaxLength(300).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.KycStatus).HasMaxLength(40).IsRequired();
        e.HasIndex(x => new { x.Provider, x.ProviderMerchantId }).IsUnique();
        e.HasIndex(x => new { x.OwnerType, x.OutletId, x.IsActive });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureFinancialDocument(EntityTypeBuilder<FinancialDocument> e)
    {
        e.ToTable("FinancialDocuments");
        e.HasKey(x => x.Id);
        e.Property(x => x.DocumentType).HasConversion<int>();
        e.Property(x => x.SupplyType).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.IssuerType).HasConversion<int>();
        e.Property(x => x.RecipientType).HasConversion<int>();
        e.Property(x => x.SourceType).HasMaxLength(100).IsRequired();
        e.Property(x => x.InvoiceNumber).HasMaxLength(80).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.PlaceOfSupplyState).HasMaxLength(100).IsRequired();
        e.Property(x => x.PlaceOfSupplyStateCode).HasMaxLength(10).IsRequired();
        e.Property(x => x.SupplierLegalName).HasMaxLength(250).IsRequired();
        e.Property(x => x.SupplierTradeName).HasMaxLength(250).IsRequired();
        e.Property(x => x.SupplierAddressLine1).HasMaxLength(500).IsRequired();
        e.Property(x => x.SupplierAddressLine2).HasMaxLength(500).IsRequired();
        e.Property(x => x.SupplierCity).HasMaxLength(100).IsRequired();
        e.Property(x => x.SupplierState).HasMaxLength(100).IsRequired();
        e.Property(x => x.SupplierStateCode).HasMaxLength(10).IsRequired();
        e.Property(x => x.SupplierPostalCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.SupplierGstin).HasMaxLength(20).IsRequired();
        e.Property(x => x.SupplierPan).HasMaxLength(20).IsRequired();
        e.Property(x => x.RecipientName).HasMaxLength(250).IsRequired();
        e.Property(x => x.RecipientAddressLine1).HasMaxLength(500).IsRequired();
        e.Property(x => x.RecipientAddressLine2).HasMaxLength(500).IsRequired();
        e.Property(x => x.RecipientCity).HasMaxLength(100).IsRequired();
        e.Property(x => x.RecipientState).HasMaxLength(100).IsRequired();
        e.Property(x => x.RecipientStateCode).HasMaxLength(10).IsRequired();
        e.Property(x => x.RecipientPostalCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.RecipientGstin).HasMaxLength(20).IsRequired();
        e.Property(x => x.RecipientPan).HasMaxLength(20).IsRequired();
        e.Property(x => x.TaxableValue).HasPrecision(18,2);
        e.Property(x => x.TotalTax).HasPrecision(18,2);
        e.Property(x => x.RoundedOffAmount).HasPrecision(18,2);
        e.Property(x => x.GrandTotal).HasPrecision(18,2);
        e.Property(x => x.Notes).HasColumnType("nvarchar(max)");
        e.HasIndex(x => new { x.IssuerType, x.IssuerId, x.FiscalYearStart, x.InvoiceNumber })
            .IsUnique()
            .HasFilter("[InvoiceNumber] <> ''")
            .HasDatabaseName("UX_FinancialDocuments_IssuerType_IssuerId_FiscalYearStart_InvoiceNumber");
        e.HasIndex(x => new { x.OutletId, x.IssueDateUtc });
        e.HasIndex(x => new { x.SourceType, x.SourceId });
        e.HasIndex(x => new { x.SupplyType, x.IssueDateUtc });
        e.HasIndex(x => new { x.IsEcoSection9_5, x.IssueDateUtc });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.OriginalDocumentId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureFinancialDocumentLine(EntityTypeBuilder<FinancialDocumentLine> e)
    {
        e.ToTable("FinancialDocumentLines");
        e.HasKey(x => x.Id);
        e.Property(x => x.ItemType).HasMaxLength(80).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        e.Property(x => x.SacCode).HasMaxLength(30).IsRequired();
        e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        e.Property(x => x.Quantity).HasPrecision(18,4);
        e.Property(x => x.UnitPrice).HasPrecision(18,2);
        e.Property(x => x.GrossAmount).HasPrecision(18,2);
        e.Property(x => x.DiscountAmount).HasPrecision(18,2);
        e.Property(x => x.TaxableAmount).HasPrecision(18,2);
        e.Property(x => x.TaxRatePercent).HasPrecision(9,4);
        e.Property(x => x.TaxAmount).HasPrecision(18,2);
        e.HasIndex(x => new { x.FinancialDocumentId, x.LineNumber }).IsUnique();
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.FinancialDocumentId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureFinancialTaxComponent(EntityTypeBuilder<FinancialTaxComponent> e)
    {
        e.ToTable("FinancialTaxComponents");
        e.HasKey(x => x.Id);
        e.Property(x => x.Component).HasConversion<int>();
        e.Property(x => x.Nature).HasConversion<int>();
        e.Property(x => x.RatePercent).HasPrecision(9,4);
        e.Property(x => x.TaxableAmount).HasPrecision(18,2);
        e.Property(x => x.TaxAmount).HasPrecision(18,2);
        e.Property(x => x.Jurisdiction).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.FinancialDocumentId, x.Component, x.FinancialDocumentLineId });
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.FinancialDocumentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocumentLine>().WithMany().HasForeignKey(x => x.FinancialDocumentLineId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePaymentAllocation(EntityTypeBuilder<PaymentAllocation> e)
    {
        e.ToTable("PaymentAllocations");
        e.HasKey(x => x.Id);
        e.Property(x => x.AllocationPurpose).HasMaxLength(100).IsRequired();
        e.Property(x => x.AllocatedAmount).HasPrecision(18,2);
        e.Property(x => x.Reference).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.PaymentTransactionId, x.FinancialDocumentId }).IsUnique();
        e.HasIndex(x => x.PaymentMerchantAccountId);
        e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.FinancialDocumentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<PaymentMerchantAccount>().WithMany().HasForeignKey(x => x.PaymentMerchantAccountId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureGatewayFee(EntityTypeBuilder<GatewayFee> e)
    {
        e.ToTable("GatewayFees");
        e.HasKey(x => x.Id);
        e.Property(x => x.ProviderFeeReference).HasMaxLength(200).IsRequired();
        e.Property(x => x.FeeAmount).HasPrecision(18,2);
        e.Property(x => x.TaxableAmount).HasPrecision(18,2);
        e.Property(x => x.GstAmount).HasPrecision(18,2);
        e.Property(x => x.TotalAmount).HasPrecision(18,2);
        e.Property(x => x.GstRatePercent).HasPrecision(9,4);
        e.HasIndex(x => new { x.PaymentTransactionId, x.ProviderFeeReference }).IsUnique();
        e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<PaymentMerchantAccount>().WithMany().HasForeignKey(x => x.PaymentMerchantAccountId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureSettlement(EntityTypeBuilder<Settlement> e)
    {
        e.ToTable("Settlements");
        e.HasKey(x => x.Id);
        e.Property(x => x.ProviderSettlementId).HasMaxLength(200).IsRequired();
        e.Property(x => x.GrossAmount).HasPrecision(18,2);
        e.Property(x => x.GatewayFeeAmount).HasPrecision(18,2);
        e.Property(x => x.GatewayFeeGstAmount).HasPrecision(18,2);
        e.Property(x => x.PlatformDeductionAmount).HasPrecision(18,2);
        e.Property(x => x.PlatformTaxAmount).HasPrecision(18,2);
        e.Property(x => x.RefundAmount).HasPrecision(18,2);
        e.Property(x => x.TaxDeductionAmount).HasPrecision(18,2);
        e.Property(x => x.AdjustmentAmount).HasPrecision(18,2);
        e.Property(x => x.NetAmount).HasPrecision(18,2);
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.Status).HasMaxLength(30).IsRequired();
        e.Property(x => x.BankReference).HasMaxLength(200).IsRequired();
        e.Property(x => x.StatementReference).HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.ProviderSettlementId).IsUnique();
        e.HasIndex(x => new { x.PaymentMerchantAccountId, x.SettlementDateUtc });
        e.HasIndex(x => new { x.OutletId, x.SettlementDateUtc });
        e.HasOne<PaymentMerchantAccount>().WithMany().HasForeignKey(x => x.PaymentMerchantAccountId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureSettlementLine(EntityTypeBuilder<SettlementLine> e)
    {
        e.ToTable("SettlementLines");
        e.HasKey(x => x.Id);
        e.Property(x => x.LineType).HasConversion<int>();
        e.Property(x => x.Amount).HasPrecision(18,2);
        e.Property(x => x.Reference).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.SettlementId, x.LineType });
        e.HasOne<Settlement>().WithMany().HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.FinancialDocumentId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureRefundTransaction(EntityTypeBuilder<RefundTransaction> e)
    {
        e.ToTable("RefundTransactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.ProviderRefundId).HasMaxLength(200).IsRequired();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.RequestedAmount).HasPrecision(18,2);
        e.Property(x => x.RefundAmount).HasPrecision(18,2);
        e.Property(x => x.RestaurantGstRefund).HasPrecision(18,2);
        e.Property(x => x.PlatformFeeRefund).HasPrecision(18,2);
        e.Property(x => x.PlatformGstRefund).HasPrecision(18,2);
        e.Property(x => x.GatewayRefundFee).HasPrecision(18,2);
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.Property(x => x.ApprovalReference).HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.ProviderRefundId)
            .IsUnique()
            .HasFilter("[ProviderRefundId] <> ''")
            .HasDatabaseName("UX_RefundTransactions_ProviderRefundId");
        e.HasIndex(x => new { x.PaymentTransactionId, x.Status });
        e.HasIndex(x => new { x.OutletId, x.RequestedAtUtc });
        e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.OriginalDocumentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialDocument>().WithMany().HasForeignKey(x => x.CreditNoteDocumentId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureLedgerAccount(EntityTypeBuilder<LedgerAccount> e)
    {
        e.ToTable("LedgerAccounts");
        e.HasKey(x => x.Id);
        e.Property(x => x.AccountCode).HasMaxLength(30).IsRequired();
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.AccountType).HasConversion<int>();
        e.Property(x => x.NormalBalance).HasConversion<int>();
        e.HasIndex(x => new { x.OutletId, x.AccountCode }).IsUnique();
        e.HasIndex(x => new { x.IsActive, x.AccountType });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureLedgerJournal(EntityTypeBuilder<LedgerJournal> e)
    {
        e.ToTable("LedgerJournals");
        e.HasKey(x => x.Id);
        e.Property(x => x.JournalNumber).HasMaxLength(80).IsRequired();
        e.Property(x => x.SourceType).HasMaxLength(100).IsRequired();
        e.Property(x => x.Memo).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Status).HasConversion<int>();
        e.HasIndex(x => x.JournalNumber).IsUnique();
        e.HasIndex(x => new { x.OutletId, x.JournalDateUtc });
        e.HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique().HasFilter("[SourceId] IS NOT NULL");
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureLedgerJournalLine(EntityTypeBuilder<LedgerJournalLine> e)
    {
        e.ToTable("LedgerJournalLines");
        e.HasKey(x => x.Id);
        e.Property(x => x.DebitAmount).HasPrecision(18,2);
        e.Property(x => x.CreditAmount).HasPrecision(18,2);
        e.Property(x => x.Reference).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.LedgerJournalId, x.LedgerAccountId });
        e.HasOne<LedgerJournal>().WithMany().HasForeignKey(x => x.LedgerJournalId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.LedgerAccountId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<FinancialTaxComponent>().WithMany().HasForeignKey(x => x.FinancialTaxComponentId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureDocumentNumberSequence(EntityTypeBuilder<DocumentNumberSequence> e)
    {
        e.ToTable("DocumentNumberSequences");
        e.HasKey(x => x.Id);
        e.Property(x => x.OwnerType).HasConversion<int>();
        e.Property(x => x.DocumentType).HasConversion<int>();
        e.Property(x => x.Prefix).HasMaxLength(30).IsRequired();
        e.Property(x => x.LastNumber).IsRequired();
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => new { x.OwnerType, x.OwnerId, x.DocumentType, x.FiscalYearStart }).IsUnique();
    }
}
