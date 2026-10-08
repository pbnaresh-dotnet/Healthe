using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

internal static class FinanceDatabaseInitializer
{
    public static async Task EnsureAsync(HealthAppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletTaxProfiles','U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletTaxProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletTaxProfiles PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        LegalName nvarchar(250) NOT NULL DEFAULT '',
        TradeName nvarchar(250) NOT NULL DEFAULT '',
        AddressLine1 nvarchar(500) NOT NULL DEFAULT '',
        AddressLine2 nvarchar(500) NOT NULL DEFAULT '',
        City nvarchar(100) NOT NULL DEFAULT '',
        State nvarchar(100) NOT NULL DEFAULT '',
        StateCode nvarchar(10) NOT NULL DEFAULT '',
        PostalCode nvarchar(20) NOT NULL DEFAULT '',
        Country nvarchar(100) NOT NULL DEFAULT 'India',
        Gstin nvarchar(20) NOT NULL DEFAULT '',
        Pan nvarchar(20) NOT NULL DEFAULT '',
        IsGstRegistered bit NOT NULL DEFAULT 0,
        IsComposition bit NOT NULL DEFAULT 0,
        RestaurantGstRate decimal(9,4) NOT NULL DEFAULT 5,
        RestaurantGstMode int NOT NULL DEFAULT 0,
        TaxOperatingMode int NOT NULL DEFAULT 1,
        EffectiveFromUtc datetime2 NOT NULL,
        EffectiveToUtc datetime2 NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.PlatformTaxProfiles','U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformTaxProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_PlatformTaxProfiles PRIMARY KEY,
        LegalName nvarchar(250) NOT NULL DEFAULT '',
        TradeName nvarchar(250) NOT NULL DEFAULT '',
        AddressLine1 nvarchar(500) NOT NULL DEFAULT '',
        AddressLine2 nvarchar(500) NOT NULL DEFAULT '',
        City nvarchar(100) NOT NULL DEFAULT '',
        State nvarchar(100) NOT NULL DEFAULT '',
        StateCode nvarchar(10) NOT NULL DEFAULT '',
        PostalCode nvarchar(20) NOT NULL DEFAULT '',
        Country nvarchar(100) NOT NULL DEFAULT 'India',
        Gstin nvarchar(20) NOT NULL DEFAULT '',
        Pan nvarchar(20) NOT NULL DEFAULT '',
        IsGstRegistered bit NOT NULL DEFAULT 0,
        EffectiveFromUtc datetime2 NOT NULL,
        EffectiveToUtc datetime2 NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.FinanceTaxRules','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinanceTaxRules
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinanceTaxRules PRIMARY KEY,
        Code nvarchar(120) NOT NULL,
        SupplyType int NOT NULL,
        TaxOperatingMode int NULL,
        TaxRatePercent decimal(9,4) NOT NULL,
        CgstRatePercent decimal(9,4) NOT NULL,
        SgstRatePercent decimal(9,4) NOT NULL,
        IgstRatePercent decimal(9,4) NOT NULL,
        IsDefault bit NOT NULL DEFAULT 0,
        Priority int NOT NULL DEFAULT 0,
        Description nvarchar(1000) NOT NULL DEFAULT '',
        EffectiveFromUtc datetime2 NOT NULL,
        EffectiveToUtc datetime2 NULL,
        IsActive bit NOT NULL DEFAULT 1
    );
END;
IF OBJECT_ID('dbo.PaymentMerchantAccounts','U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentMerchantAccounts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_PaymentMerchantAccounts PRIMARY KEY,
        OwnerType int NOT NULL,
        OutletId uniqueidentifier NULL,
        Provider nvarchar(50) NOT NULL,
        ProviderMerchantId nvarchar(200) NOT NULL,
        SettlementAccountReference nvarchar(300) NOT NULL,
        Currency nvarchar(3) NOT NULL DEFAULT 'INR',
        KycStatus nvarchar(40) NOT NULL DEFAULT 'Pending',
        IsActive bit NOT NULL DEFAULT 1,
        EffectiveFromUtc datetime2 NOT NULL,
        EffectiveToUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.FinancialDocuments','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialDocuments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancialDocuments PRIMARY KEY,
        OutletId uniqueidentifier NULL,
        DocumentType int NOT NULL,
        SupplyType int NOT NULL,
        Status int NOT NULL DEFAULT 1,
        IssuerType int NOT NULL,
        IssuerId uniqueidentifier NOT NULL,
        RecipientType int NOT NULL,
        RecipientId uniqueidentifier NULL,
        SourceType nvarchar(100) NOT NULL DEFAULT '',
        SourceId uniqueidentifier NULL,
        InvoiceNumber nvarchar(80) NOT NULL DEFAULT '',
        FiscalYearStart int NOT NULL,
        IssueDateUtc datetime2 NOT NULL,
        SupplyDateUtc datetime2 NOT NULL,
        Currency nvarchar(3) NOT NULL DEFAULT 'INR',
        PlaceOfSupplyState nvarchar(100) NOT NULL DEFAULT '',
        PlaceOfSupplyStateCode nvarchar(10) NOT NULL DEFAULT '',
        ReverseCharge bit NOT NULL DEFAULT 0,
        IsEcoSection9_5 bit NOT NULL DEFAULT 0,
        OriginalDocumentId uniqueidentifier NULL,
        SupplierLegalName nvarchar(250) NOT NULL DEFAULT '',
        SupplierTradeName nvarchar(250) NOT NULL DEFAULT '',
        SupplierAddressLine1 nvarchar(500) NOT NULL DEFAULT '',
        SupplierAddressLine2 nvarchar(500) NOT NULL DEFAULT '',
        SupplierCity nvarchar(100) NOT NULL DEFAULT '',
        SupplierState nvarchar(100) NOT NULL DEFAULT '',
        SupplierStateCode nvarchar(10) NOT NULL DEFAULT '',
        SupplierPostalCode nvarchar(20) NOT NULL DEFAULT '',
        SupplierGstin nvarchar(20) NOT NULL DEFAULT '',
        SupplierPan nvarchar(20) NOT NULL DEFAULT '',
        RecipientName nvarchar(250) NOT NULL DEFAULT '',
        RecipientAddressLine1 nvarchar(500) NOT NULL DEFAULT '',
        RecipientAddressLine2 nvarchar(500) NOT NULL DEFAULT '',
        RecipientCity nvarchar(100) NOT NULL DEFAULT '',
        RecipientState nvarchar(100) NOT NULL DEFAULT '',
        RecipientStateCode nvarchar(10) NOT NULL DEFAULT '',
        RecipientPostalCode nvarchar(20) NOT NULL DEFAULT '',
        RecipientGstin nvarchar(20) NOT NULL DEFAULT '',
        RecipientPan nvarchar(20) NOT NULL DEFAULT '',
        TaxableValue decimal(18,2) NOT NULL DEFAULT 0,
        TotalTax decimal(18,2) NOT NULL DEFAULT 0,
        RoundedOffAmount decimal(18,2) NOT NULL DEFAULT 0,
        GrandTotal decimal(18,2) NOT NULL DEFAULT 0,
        Notes nvarchar(max) NOT NULL DEFAULT '',
        IssuedAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.FinancialDocumentLines','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialDocumentLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancialDocumentLines PRIMARY KEY,
        FinancialDocumentId uniqueidentifier NOT NULL,
        LineNumber int NOT NULL,
        ItemType nvarchar(80) NOT NULL DEFAULT '',
        Description nvarchar(1000) NOT NULL DEFAULT '',
        SacCode nvarchar(30) NOT NULL DEFAULT '',
        Quantity decimal(18,4) NOT NULL DEFAULT 1,
        Unit nvarchar(30) NOT NULL DEFAULT 'unit',
        UnitPrice decimal(18,2) NOT NULL DEFAULT 0,
        GrossAmount decimal(18,2) NOT NULL DEFAULT 0,
        DiscountAmount decimal(18,2) NOT NULL DEFAULT 0,
        TaxableAmount decimal(18,2) NOT NULL DEFAULT 0,
        TaxRatePercent decimal(9,4) NOT NULL DEFAULT 0,
        TaxAmount decimal(18,2) NOT NULL DEFAULT 0,
        IsTaxInclusive bit NOT NULL DEFAULT 0
    );
END;
IF OBJECT_ID('dbo.FinancialTaxComponents','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialTaxComponents
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancialTaxComponents PRIMARY KEY,
        FinancialDocumentId uniqueidentifier NOT NULL,
        FinancialDocumentLineId uniqueidentifier NULL,
        Component int NOT NULL,
        Nature int NOT NULL DEFAULT 1,
        RatePercent decimal(9,4) NOT NULL,
        TaxableAmount decimal(18,2) NOT NULL,
        TaxAmount decimal(18,2) NOT NULL,
        Jurisdiction nvarchar(100) NOT NULL DEFAULT '',
        IsSection9_5Liability bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.PaymentAllocations','U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentAllocations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_PaymentAllocations PRIMARY KEY,
        PaymentTransactionId uniqueidentifier NOT NULL,
        FinancialDocumentId uniqueidentifier NOT NULL,
        PaymentMerchantAccountId uniqueidentifier NOT NULL,
        AllocationPurpose nvarchar(100) NOT NULL DEFAULT '',
        AllocatedAmount decimal(18,2) NOT NULL,
        AllocatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Reference nvarchar(200) NOT NULL DEFAULT ''
    );
END;
IF OBJECT_ID('dbo.GatewayFees','U') IS NULL
BEGIN
    CREATE TABLE dbo.GatewayFees
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_GatewayFees PRIMARY KEY,
        PaymentTransactionId uniqueidentifier NOT NULL,
        PaymentMerchantAccountId uniqueidentifier NOT NULL,
        ProviderFeeReference nvarchar(200) NOT NULL,
        FeeAmount decimal(18,2) NOT NULL,
        TaxableAmount decimal(18,2) NOT NULL,
        GstAmount decimal(18,2) NOT NULL,
        TotalAmount decimal(18,2) NOT NULL,
        GstRatePercent decimal(9,4) NOT NULL,
        ChargedAtUtc datetime2 NOT NULL
    );
END;
IF OBJECT_ID('dbo.Settlements','U') IS NULL
BEGIN
    CREATE TABLE dbo.Settlements
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Settlements PRIMARY KEY,
        PaymentMerchantAccountId uniqueidentifier NOT NULL,
        OutletId uniqueidentifier NULL,
        ProviderSettlementId nvarchar(200) NOT NULL,
        SettlementDateUtc datetime2 NOT NULL,
        BankValueDateUtc datetime2 NULL,
        GrossAmount decimal(18,2) NOT NULL,
        GatewayFeeAmount decimal(18,2) NOT NULL,
        GatewayFeeGstAmount decimal(18,2) NOT NULL,
        PlatformDeductionAmount decimal(18,2) NOT NULL,
        PlatformTaxAmount decimal(18,2) NOT NULL,
        RefundAmount decimal(18,2) NOT NULL,
        TaxDeductionAmount decimal(18,2) NOT NULL,
        AdjustmentAmount decimal(18,2) NOT NULL,
        NetAmount decimal(18,2) NOT NULL,
        Currency nvarchar(3) NOT NULL DEFAULT 'INR',
        Status nvarchar(30) NOT NULL DEFAULT 'Pending',
        BankReference nvarchar(200) NOT NULL DEFAULT '',
        StatementReference nvarchar(200) NOT NULL DEFAULT '',
        ReconciledAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.SettlementLines','U') IS NULL
BEGIN
    CREATE TABLE dbo.SettlementLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_SettlementLines PRIMARY KEY,
        SettlementId uniqueidentifier NOT NULL,
        PaymentTransactionId uniqueidentifier NULL,
        FinancialDocumentId uniqueidentifier NULL,
        LineType int NOT NULL,
        Amount decimal(18,2) NOT NULL,
        Reference nvarchar(200) NOT NULL DEFAULT ''
    );
END;
IF OBJECT_ID('dbo.RefundTransactions','U') IS NULL
BEGIN
    CREATE TABLE dbo.RefundTransactions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_RefundTransactions PRIMARY KEY,
        PaymentTransactionId uniqueidentifier NOT NULL,
        OutletId uniqueidentifier NULL,
        CustomerId uniqueidentifier NULL,
        OriginalDocumentId uniqueidentifier NOT NULL,
        CreditNoteDocumentId uniqueidentifier NULL,
        ProviderRefundId nvarchar(200) NOT NULL,
        Status int NOT NULL DEFAULT 1,
        RequestedAmount decimal(18,2) NOT NULL,
        RefundAmount decimal(18,2) NOT NULL,
        RestaurantGstRefund decimal(18,2) NOT NULL,
        PlatformFeeRefund decimal(18,2) NOT NULL,
        PlatformGstRefund decimal(18,2) NOT NULL,
        GatewayRefundFee decimal(18,2) NOT NULL,
        Reason nvarchar(1000) NOT NULL DEFAULT '',
        ApprovalReference nvarchar(200) NOT NULL DEFAULT '',
        RequestedAtUtc datetime2 NOT NULL,
        ProcessedAtUtc datetime2 NULL,
        ReconciledAtUtc datetime2 NULL
    );
END;
IF OBJECT_ID('dbo.LedgerAccounts','U') IS NULL
BEGIN
    CREATE TABLE dbo.LedgerAccounts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_LedgerAccounts PRIMARY KEY,
        AccountCode nvarchar(30) NOT NULL,
        Name nvarchar(150) NOT NULL,
        AccountType int NOT NULL,
        NormalBalance int NOT NULL,
        OutletId uniqueidentifier NULL,
        IsSystem bit NOT NULL DEFAULT 0,
        IsActive bit NOT NULL DEFAULT 1
    );
END;
IF OBJECT_ID('dbo.LedgerJournals','U') IS NULL
BEGIN
    CREATE TABLE dbo.LedgerJournals
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_LedgerJournals PRIMARY KEY,
        OutletId uniqueidentifier NULL,
        JournalNumber nvarchar(80) NOT NULL,
        SourceType nvarchar(100) NOT NULL DEFAULT '',
        SourceId uniqueidentifier NULL,
        JournalDateUtc datetime2 NOT NULL,
        Memo nvarchar(1000) NOT NULL DEFAULT '',
        Status int NOT NULL DEFAULT 1,
        PostedAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.LedgerJournalLines','U') IS NULL
BEGIN
    CREATE TABLE dbo.LedgerJournalLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_LedgerJournalLines PRIMARY KEY,
        LedgerJournalId uniqueidentifier NOT NULL,
        LedgerAccountId uniqueidentifier NOT NULL,
        FinancialTaxComponentId uniqueidentifier NULL,
        DebitAmount decimal(18,2) NOT NULL DEFAULT 0,
        CreditAmount decimal(18,2) NOT NULL DEFAULT 0,
        Reference nvarchar(200) NOT NULL DEFAULT ''
    );
END;
IF OBJECT_ID('dbo.DocumentNumberSequences','U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentNumberSequences
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DocumentNumberSequences PRIMARY KEY,
        OwnerType int NOT NULL,
        OwnerId uniqueidentifier NOT NULL,
        DocumentType int NOT NULL,
        FiscalYearStart int NOT NULL,
        Prefix nvarchar(30) NOT NULL DEFAULT '',
        LastNumber bigint NOT NULL DEFAULT 0,
        RowVersion rowversion NOT NULL
    );
END;
IF OBJECT_ID('dbo.FinancePolicyDocuments','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancePolicyDocuments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePolicyDocuments PRIMARY KEY,
        Code nvarchar(120) NOT NULL,
        Title nvarchar(250) NOT NULL,
        Description nvarchar(1000) NOT NULL DEFAULT '',
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.FinancePolicyDocumentVersions','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancePolicyDocumentVersions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePolicyDocumentVersions PRIMARY KEY,
        FinancePolicyDocumentId uniqueidentifier NOT NULL,
        Version nvarchar(30) NOT NULL,
        Status int NOT NULL DEFAULT 1,
        EffectiveFromUtc datetime2 NOT NULL,
        EffectiveToUtc datetime2 NULL,
        ChangeSummary nvarchar(1000) NOT NULL DEFAULT '',
        ChangeReason nvarchar(2000) NOT NULL DEFAULT '',
        SourceCodeReference nvarchar(200) NOT NULL DEFAULT '',
        ContentHash nvarchar(128) NOT NULL DEFAULT '',
        PreviousVersionId uniqueidentifier NULL,
        CreatedByUserId uniqueidentifier NULL,
        ReviewedByUserId uniqueidentifier NULL,
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ReviewedAtUtc datetime2 NULL,
        PublishedAtUtc datetime2 NULL
    );
END;
IF OBJECT_ID('dbo.FinancePolicyDocumentSections','U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancePolicyDocumentSections
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePolicyDocumentSections PRIMARY KEY,
        FinancePolicyDocumentVersionId uniqueidentifier NOT NULL,
        SectionCode nvarchar(120) NOT NULL,
        Title nvarchar(250) NOT NULL,
        DisplayOrder int NOT NULL DEFAULT 0,
        ContentMarkdown nvarchar(max) NOT NULL DEFAULT '',
        CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.PaymentTransactions','MerchantAccountId') IS NULL
    ALTER TABLE dbo.PaymentTransactions ADD MerchantAccountId uniqueidentifier NULL;
IF COL_LENGTH('dbo.PaymentTransactions','Purpose') IS NULL
    ALTER TABLE dbo.PaymentTransactions ADD Purpose nvarchar(100) NOT NULL CONSTRAINT DF_PaymentTransactions_Purpose DEFAULT 'CustomerSubscription';
IF COL_LENGTH('dbo.PaymentTransactions','ReconciliationStatus') IS NULL
    ALTER TABLE dbo.PaymentTransactions ADD ReconciliationStatus nvarchar(40) NOT NULL CONSTRAINT DF_PaymentTransactions_ReconciliationStatus DEFAULT 'Unreconciled';
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletTaxProfiles_OutletId_EffectiveFromUtc' AND object_id=OBJECT_ID('dbo.OutletTaxProfiles'))
    CREATE UNIQUE INDEX IX_OutletTaxProfiles_OutletId_EffectiveFromUtc ON dbo.OutletTaxProfiles(OutletId, EffectiveFromUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletTaxProfiles_OutletId_IsActive' AND object_id=OBJECT_ID('dbo.OutletTaxProfiles'))
    CREATE INDEX IX_OutletTaxProfiles_OutletId_IsActive ON dbo.OutletTaxProfiles(OutletId, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_OutletTaxProfiles_Outlets' AND parent_object_id=OBJECT_ID('dbo.OutletTaxProfiles'))
    ALTER TABLE dbo.OutletTaxProfiles ADD CONSTRAINT FK_OutletTaxProfiles_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PlatformTaxProfiles_EffectiveFromUtc' AND object_id=OBJECT_ID('dbo.PlatformTaxProfiles'))
    CREATE UNIQUE INDEX IX_PlatformTaxProfiles_EffectiveFromUtc ON dbo.PlatformTaxProfiles(EffectiveFromUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PlatformTaxProfiles_IsActive' AND object_id=OBJECT_ID('dbo.PlatformTaxProfiles'))
    CREATE INDEX IX_PlatformTaxProfiles_IsActive ON dbo.PlatformTaxProfiles(IsActive);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinanceTaxRules_Code' AND object_id=OBJECT_ID('dbo.FinanceTaxRules'))
    CREATE UNIQUE INDEX IX_FinanceTaxRules_Code ON dbo.FinanceTaxRules(Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinanceTaxRules_SupplyType_TaxOperatingMode_EffectiveFromUtc' AND object_id=OBJECT_ID('dbo.FinanceTaxRules'))
    CREATE INDEX IX_FinanceTaxRules_SupplyType_TaxOperatingMode_EffectiveFromUtc ON dbo.FinanceTaxRules(SupplyType, TaxOperatingMode, EffectiveFromUtc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PaymentMerchantAccounts_Provider_ProviderMerchantId' AND object_id=OBJECT_ID('dbo.PaymentMerchantAccounts'))
    CREATE UNIQUE INDEX IX_PaymentMerchantAccounts_Provider_ProviderMerchantId ON dbo.PaymentMerchantAccounts(Provider, ProviderMerchantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PaymentMerchantAccounts_OwnerType_OutletId_IsActive' AND object_id=OBJECT_ID('dbo.PaymentMerchantAccounts'))
    CREATE INDEX IX_PaymentMerchantAccounts_OwnerType_OutletId_IsActive ON dbo.PaymentMerchantAccounts(OwnerType, OutletId, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PaymentMerchantAccounts_Outlets' AND parent_object_id=OBJECT_ID('dbo.PaymentMerchantAccounts'))
    ALTER TABLE dbo.PaymentMerchantAccounts ADD CONSTRAINT FK_PaymentMerchantAccounts_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocuments_IssuerType_IssuerId_FiscalYearStart_InvoiceNumber' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    DROP INDEX IX_FinancialDocuments_IssuerType_IssuerId_FiscalYearStart_InvoiceNumber ON dbo.FinancialDocuments;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_FinancialDocuments_IssuerType_IssuerId_FiscalYearStart_InvoiceNumber' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    CREATE UNIQUE INDEX UX_FinancialDocuments_IssuerType_IssuerId_FiscalYearStart_InvoiceNumber
    ON dbo.FinancialDocuments(IssuerType, IssuerId, FiscalYearStart, InvoiceNumber)
    WHERE InvoiceNumber <> '';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocuments_OutletId_IssueDateUtc' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    CREATE INDEX IX_FinancialDocuments_OutletId_IssueDateUtc ON dbo.FinancialDocuments(OutletId, IssueDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocuments_SourceType_SourceId' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    CREATE INDEX IX_FinancialDocuments_SourceType_SourceId ON dbo.FinancialDocuments(SourceType, SourceId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocuments_SupplyType_IssueDateUtc' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    CREATE INDEX IX_FinancialDocuments_SupplyType_IssueDateUtc ON dbo.FinancialDocuments(SupplyType, IssueDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocuments_IsEcoSection9_5_IssueDateUtc' AND object_id=OBJECT_ID('dbo.FinancialDocuments'))
    CREATE INDEX IX_FinancialDocuments_IsEcoSection9_5_IssueDateUtc ON dbo.FinancialDocuments(IsEcoSection9_5, IssueDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancialDocuments_Outlets' AND parent_object_id=OBJECT_ID('dbo.FinancialDocuments'))
    ALTER TABLE dbo.FinancialDocuments ADD CONSTRAINT FK_FinancialDocuments_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancialDocuments_OriginalDocument' AND parent_object_id=OBJECT_ID('dbo.FinancialDocuments'))
    ALTER TABLE dbo.FinancialDocuments ADD CONSTRAINT FK_FinancialDocuments_OriginalDocument FOREIGN KEY(OriginalDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialDocumentLines_FinancialDocumentId_LineNumber' AND object_id=OBJECT_ID('dbo.FinancialDocumentLines'))
    CREATE UNIQUE INDEX IX_FinancialDocumentLines_FinancialDocumentId_LineNumber ON dbo.FinancialDocumentLines(FinancialDocumentId, LineNumber);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancialDocumentLines_FinancialDocuments' AND parent_object_id=OBJECT_ID('dbo.FinancialDocumentLines'))
    ALTER TABLE dbo.FinancialDocumentLines ADD CONSTRAINT FK_FinancialDocumentLines_FinancialDocuments FOREIGN KEY(FinancialDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancialTaxComponents_FinancialDocumentId_Component_LineId' AND object_id=OBJECT_ID('dbo.FinancialTaxComponents'))
    CREATE INDEX IX_FinancialTaxComponents_FinancialDocumentId_Component_LineId ON dbo.FinancialTaxComponents(FinancialDocumentId, Component, FinancialDocumentLineId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancialTaxComponents_FinancialDocuments' AND parent_object_id=OBJECT_ID('dbo.FinancialTaxComponents'))
    ALTER TABLE dbo.FinancialTaxComponents ADD CONSTRAINT FK_FinancialTaxComponents_FinancialDocuments FOREIGN KEY(FinancialDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancialTaxComponents_FinancialDocumentLines' AND parent_object_id=OBJECT_ID('dbo.FinancialTaxComponents'))
    ALTER TABLE dbo.FinancialTaxComponents ADD CONSTRAINT FK_FinancialTaxComponents_FinancialDocumentLines FOREIGN KEY(FinancialDocumentLineId) REFERENCES dbo.FinancialDocumentLines(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PaymentAllocations_PaymentTransactionId_FinancialDocumentId' AND object_id=OBJECT_ID('dbo.PaymentAllocations'))
    CREATE UNIQUE INDEX IX_PaymentAllocations_PaymentTransactionId_FinancialDocumentId ON dbo.PaymentAllocations(PaymentTransactionId, FinancialDocumentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PaymentAllocations_PaymentMerchantAccountId' AND object_id=OBJECT_ID('dbo.PaymentAllocations'))
    CREATE INDEX IX_PaymentAllocations_PaymentMerchantAccountId ON dbo.PaymentAllocations(PaymentMerchantAccountId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PaymentAllocations_PaymentTransactions' AND parent_object_id=OBJECT_ID('dbo.PaymentAllocations'))
    ALTER TABLE dbo.PaymentAllocations ADD CONSTRAINT FK_PaymentAllocations_PaymentTransactions FOREIGN KEY(PaymentTransactionId) REFERENCES dbo.PaymentTransactions(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PaymentAllocations_FinancialDocuments' AND parent_object_id=OBJECT_ID('dbo.PaymentAllocations'))
    ALTER TABLE dbo.PaymentAllocations ADD CONSTRAINT FK_PaymentAllocations_FinancialDocuments FOREIGN KEY(FinancialDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PaymentAllocations_PaymentMerchantAccounts' AND parent_object_id=OBJECT_ID('dbo.PaymentAllocations'))
    ALTER TABLE dbo.PaymentAllocations ADD CONSTRAINT FK_PaymentAllocations_PaymentMerchantAccounts FOREIGN KEY(PaymentMerchantAccountId) REFERENCES dbo.PaymentMerchantAccounts(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GatewayFees_PaymentTransactionId_ProviderFeeReference' AND object_id=OBJECT_ID('dbo.GatewayFees'))
    CREATE UNIQUE INDEX IX_GatewayFees_PaymentTransactionId_ProviderFeeReference ON dbo.GatewayFees(PaymentTransactionId, ProviderFeeReference);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_GatewayFees_PaymentTransactions' AND parent_object_id=OBJECT_ID('dbo.GatewayFees'))
    ALTER TABLE dbo.GatewayFees ADD CONSTRAINT FK_GatewayFees_PaymentTransactions FOREIGN KEY(PaymentTransactionId) REFERENCES dbo.PaymentTransactions(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_GatewayFees_PaymentMerchantAccounts' AND parent_object_id=OBJECT_ID('dbo.GatewayFees'))
    ALTER TABLE dbo.GatewayFees ADD CONSTRAINT FK_GatewayFees_PaymentMerchantAccounts FOREIGN KEY(PaymentMerchantAccountId) REFERENCES dbo.PaymentMerchantAccounts(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Settlements_ProviderSettlementId' AND object_id=OBJECT_ID('dbo.Settlements'))
    CREATE UNIQUE INDEX IX_Settlements_ProviderSettlementId ON dbo.Settlements(ProviderSettlementId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Settlements_PaymentMerchantAccountId_SettlementDateUtc' AND object_id=OBJECT_ID('dbo.Settlements'))
    CREATE INDEX IX_Settlements_PaymentMerchantAccountId_SettlementDateUtc ON dbo.Settlements(PaymentMerchantAccountId, SettlementDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Settlements_OutletId_SettlementDateUtc' AND object_id=OBJECT_ID('dbo.Settlements'))
    CREATE INDEX IX_Settlements_OutletId_SettlementDateUtc ON dbo.Settlements(OutletId, SettlementDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Settlements_PaymentMerchantAccounts' AND parent_object_id=OBJECT_ID('dbo.Settlements'))
    ALTER TABLE dbo.Settlements ADD CONSTRAINT FK_Settlements_PaymentMerchantAccounts FOREIGN KEY(PaymentMerchantAccountId) REFERENCES dbo.PaymentMerchantAccounts(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Settlements_Outlets' AND parent_object_id=OBJECT_ID('dbo.Settlements'))
    ALTER TABLE dbo.Settlements ADD CONSTRAINT FK_Settlements_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SettlementLines_SettlementId_LineType' AND object_id=OBJECT_ID('dbo.SettlementLines'))
    CREATE INDEX IX_SettlementLines_SettlementId_LineType ON dbo.SettlementLines(SettlementId, LineType);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_SettlementLines_Settlements' AND parent_object_id=OBJECT_ID('dbo.SettlementLines'))
    ALTER TABLE dbo.SettlementLines ADD CONSTRAINT FK_SettlementLines_Settlements FOREIGN KEY(SettlementId) REFERENCES dbo.Settlements(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_SettlementLines_PaymentTransactions' AND parent_object_id=OBJECT_ID('dbo.SettlementLines'))
    ALTER TABLE dbo.SettlementLines ADD CONSTRAINT FK_SettlementLines_PaymentTransactions FOREIGN KEY(PaymentTransactionId) REFERENCES dbo.PaymentTransactions(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_SettlementLines_FinancialDocuments' AND parent_object_id=OBJECT_ID('dbo.SettlementLines'))
    ALTER TABLE dbo.SettlementLines ADD CONSTRAINT FK_SettlementLines_FinancialDocuments FOREIGN KEY(FinancialDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RefundTransactions_ProviderRefundId' AND object_id=OBJECT_ID('dbo.RefundTransactions'))
    DROP INDEX IX_RefundTransactions_ProviderRefundId ON dbo.RefundTransactions;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_RefundTransactions_ProviderRefundId' AND object_id=OBJECT_ID('dbo.RefundTransactions'))
    CREATE UNIQUE INDEX UX_RefundTransactions_ProviderRefundId
    ON dbo.RefundTransactions(ProviderRefundId)
    WHERE ProviderRefundId <> '';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RefundTransactions_PaymentTransactionId_Status' AND object_id=OBJECT_ID('dbo.RefundTransactions'))
    CREATE INDEX IX_RefundTransactions_PaymentTransactionId_Status ON dbo.RefundTransactions(PaymentTransactionId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RefundTransactions_OutletId_RequestedAtUtc' AND object_id=OBJECT_ID('dbo.RefundTransactions'))
    CREATE INDEX IX_RefundTransactions_OutletId_RequestedAtUtc ON dbo.RefundTransactions(OutletId, RequestedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_RefundTransactions_PaymentTransactions' AND parent_object_id=OBJECT_ID('dbo.RefundTransactions'))
    ALTER TABLE dbo.RefundTransactions ADD CONSTRAINT FK_RefundTransactions_PaymentTransactions FOREIGN KEY(PaymentTransactionId) REFERENCES dbo.PaymentTransactions(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_RefundTransactions_Outlets' AND parent_object_id=OBJECT_ID('dbo.RefundTransactions'))
    ALTER TABLE dbo.RefundTransactions ADD CONSTRAINT FK_RefundTransactions_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_RefundTransactions_Users' AND parent_object_id=OBJECT_ID('dbo.RefundTransactions'))
    ALTER TABLE dbo.RefundTransactions ADD CONSTRAINT FK_RefundTransactions_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_RefundTransactions_OriginalDocument' AND parent_object_id=OBJECT_ID('dbo.RefundTransactions'))
    ALTER TABLE dbo.RefundTransactions ADD CONSTRAINT FK_RefundTransactions_OriginalDocument FOREIGN KEY(OriginalDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_RefundTransactions_CreditNoteDocument' AND parent_object_id=OBJECT_ID('dbo.RefundTransactions'))
    ALTER TABLE dbo.RefundTransactions ADD CONSTRAINT FK_RefundTransactions_CreditNoteDocument FOREIGN KEY(CreditNoteDocumentId) REFERENCES dbo.FinancialDocuments(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LedgerAccounts_OutletId_AccountCode' AND object_id=OBJECT_ID('dbo.LedgerAccounts'))
    CREATE UNIQUE INDEX IX_LedgerAccounts_OutletId_AccountCode ON dbo.LedgerAccounts(OutletId, AccountCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LedgerAccounts_IsActive_AccountType' AND object_id=OBJECT_ID('dbo.LedgerAccounts'))
    CREATE INDEX IX_LedgerAccounts_IsActive_AccountType ON dbo.LedgerAccounts(IsActive, AccountType);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LedgerAccounts_Outlets' AND parent_object_id=OBJECT_ID('dbo.LedgerAccounts'))
    ALTER TABLE dbo.LedgerAccounts ADD CONSTRAINT FK_LedgerAccounts_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LedgerJournals_JournalNumber' AND object_id=OBJECT_ID('dbo.LedgerJournals'))
    CREATE UNIQUE INDEX IX_LedgerJournals_JournalNumber ON dbo.LedgerJournals(JournalNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LedgerJournals_OutletId_JournalDateUtc' AND object_id=OBJECT_ID('dbo.LedgerJournals'))
    CREATE INDEX IX_LedgerJournals_OutletId_JournalDateUtc ON dbo.LedgerJournals(OutletId, JournalDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LedgerJournals_SourceType_SourceId' AND object_id=OBJECT_ID('dbo.LedgerJournals'))
    CREATE UNIQUE INDEX UX_LedgerJournals_SourceType_SourceId ON dbo.LedgerJournals(SourceType, SourceId) WHERE SourceId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LedgerJournals_Outlets' AND parent_object_id=OBJECT_ID('dbo.LedgerJournals'))
    ALTER TABLE dbo.LedgerJournals ADD CONSTRAINT FK_LedgerJournals_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LedgerJournalLines_JournalId_AccountId' AND object_id=OBJECT_ID('dbo.LedgerJournalLines'))
    CREATE INDEX IX_LedgerJournalLines_JournalId_AccountId ON dbo.LedgerJournalLines(LedgerJournalId, LedgerAccountId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LedgerJournalLines_LedgerJournals' AND parent_object_id=OBJECT_ID('dbo.LedgerJournalLines'))
    ALTER TABLE dbo.LedgerJournalLines ADD CONSTRAINT FK_LedgerJournalLines_LedgerJournals FOREIGN KEY(LedgerJournalId) REFERENCES dbo.LedgerJournals(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LedgerJournalLines_LedgerAccounts' AND parent_object_id=OBJECT_ID('dbo.LedgerJournalLines'))
    ALTER TABLE dbo.LedgerJournalLines ADD CONSTRAINT FK_LedgerJournalLines_LedgerAccounts FOREIGN KEY(LedgerAccountId) REFERENCES dbo.LedgerAccounts(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LedgerJournalLines_FinancialTaxComponents' AND parent_object_id=OBJECT_ID('dbo.LedgerJournalLines'))
    ALTER TABLE dbo.LedgerJournalLines ADD CONSTRAINT FK_LedgerJournalLines_FinancialTaxComponents FOREIGN KEY(FinancialTaxComponentId) REFERENCES dbo.FinancialTaxComponents(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DocumentNumberSequences_OwnerType_OwnerId_DocumentType_FiscalYearStart' AND object_id=OBJECT_ID('dbo.DocumentNumberSequences'))
    CREATE UNIQUE INDEX IX_DocumentNumberSequences_OwnerType_OwnerId_DocumentType_FiscalYearStart ON dbo.DocumentNumberSequences(OwnerType, OwnerId, DocumentType, FiscalYearStart);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocuments_Code' AND object_id=OBJECT_ID('dbo.FinancePolicyDocuments'))
    CREATE UNIQUE INDEX IX_FinancePolicyDocuments_Code ON dbo.FinancePolicyDocuments(Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocuments_IsActive' AND object_id=OBJECT_ID('dbo.FinancePolicyDocuments'))
    CREATE INDEX IX_FinancePolicyDocuments_IsActive ON dbo.FinancePolicyDocuments(IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocumentVersions_DocumentId_Version' AND object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    CREATE UNIQUE INDEX IX_FinancePolicyDocumentVersions_DocumentId_Version ON dbo.FinancePolicyDocumentVersions(FinancePolicyDocumentId, Version);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocumentVersions_DocumentId_Status_EffectiveFromUtc' AND object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    CREATE INDEX IX_FinancePolicyDocumentVersions_DocumentId_Status_EffectiveFromUtc ON dbo.FinancePolicyDocumentVersions(FinancePolicyDocumentId, Status, EffectiveFromUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocumentVersions_PreviousVersionId' AND object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    CREATE INDEX IX_FinancePolicyDocumentVersions_PreviousVersionId ON dbo.FinancePolicyDocumentVersions(PreviousVersionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocumentSections_VersionId_SectionCode' AND object_id=OBJECT_ID('dbo.FinancePolicyDocumentSections'))
    CREATE UNIQUE INDEX IX_FinancePolicyDocumentSections_VersionId_SectionCode ON dbo.FinancePolicyDocumentSections(FinancePolicyDocumentVersionId, SectionCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinancePolicyDocumentSections_VersionId_DisplayOrder' AND object_id=OBJECT_ID('dbo.FinancePolicyDocumentSections'))
    CREATE INDEX IX_FinancePolicyDocumentSections_VersionId_DisplayOrder ON dbo.FinancePolicyDocumentSections(FinancePolicyDocumentVersionId, DisplayOrder);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancePolicyDocumentVersions_Documents' AND parent_object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    ALTER TABLE dbo.FinancePolicyDocumentVersions ADD CONSTRAINT FK_FinancePolicyDocumentVersions_Documents FOREIGN KEY(FinancePolicyDocumentId) REFERENCES dbo.FinancePolicyDocuments(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancePolicyDocumentVersions_PreviousVersion' AND parent_object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    ALTER TABLE dbo.FinancePolicyDocumentVersions ADD CONSTRAINT FK_FinancePolicyDocumentVersions_PreviousVersion FOREIGN KEY(PreviousVersionId) REFERENCES dbo.FinancePolicyDocumentVersions(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancePolicyDocumentVersions_CreatedByUser' AND parent_object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    ALTER TABLE dbo.FinancePolicyDocumentVersions ADD CONSTRAINT FK_FinancePolicyDocumentVersions_CreatedByUser FOREIGN KEY(CreatedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancePolicyDocumentVersions_ReviewedByUser' AND parent_object_id=OBJECT_ID('dbo.FinancePolicyDocumentVersions'))
    ALTER TABLE dbo.FinancePolicyDocumentVersions ADD CONSTRAINT FK_FinancePolicyDocumentVersions_ReviewedByUser FOREIGN KEY(ReviewedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FinancePolicyDocumentSections_Versions' AND parent_object_id=OBJECT_ID('dbo.FinancePolicyDocumentSections'))
    ALTER TABLE dbo.FinancePolicyDocumentSections ADD CONSTRAINT FK_FinancePolicyDocumentSections_Versions FOREIGN KEY(FinancePolicyDocumentVersionId) REFERENCES dbo.FinancePolicyDocumentVersions(Id) ON DELETE NO ACTION;
", cancellationToken);

        // Bootstrap tax configuration for every existing outlet without changing its
        // current restaurant rate/mode. New tenants will be populated by the tax-profile
        // service in the transaction-posting phase.
        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO dbo.OutletTaxProfiles
(
    Id, OutletId, LegalName, TradeName, AddressLine1, AddressLine2, City, State, StateCode,
    PostalCode, Country, Gstin, Pan, IsGstRegistered, IsComposition, RestaurantGstRate,
    RestaurantGstMode, TaxOperatingMode, EffectiveFromUtc, IsActive, CreatedAtUtc
)
SELECT
    NEWID(), o.Id, o.Name, o.Name, '', '', o.City, o.State, '', o.Pincode, 'India', '', '',
    0, 0, o.RestaurantGstRate, o.RestaurantGstMode, 1, CAST('2000-01-01' AS datetime2), 1, SYSUTCDATETIME()
FROM dbo.Outlets o
WHERE NOT EXISTS (SELECT 1 FROM dbo.OutletTaxProfiles p WHERE p.OutletId = o.Id);
", cancellationToken);

        // Seed only product defaults. Exact GST/SAC classification remains an explicit
        // production configuration decision and is documented in the admin help page.
        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM dbo.FinanceTaxRules WHERE Code='RESTAURANT_STANDARD_5')
    INSERT dbo.FinanceTaxRules
    (Id, Code, SupplyType, TaxOperatingMode, TaxRatePercent, CgstRatePercent, SgstRatePercent, IgstRatePercent, IsDefault, Priority, Description, EffectiveFromUtc, IsActive)
    VALUES
    (NEWID(), 'RESTAURANT_STANDARD_5', 1, NULL, 5, 2.5, 2.5, 5, 1, 100,
     'Product default: standard restaurant service at 5%; validate applicability before production.', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FinanceTaxRules WHERE Code='PLATFORM_SAAS_STANDARD_18')
    INSERT dbo.FinanceTaxRules
    (Id, Code, SupplyType, TaxOperatingMode, TaxRatePercent, CgstRatePercent, SgstRatePercent, IgstRatePercent, IsDefault, Priority, Description, EffectiveFromUtc, IsActive)
    VALUES
    (NEWID(), 'PLATFORM_SAAS_STANDARD_18', 2, NULL, 18, 9, 9, 18, 1, 100,
     'Product default: HealthApp SaaS service at 18%; exact SAC/treatment requires validation.', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FinanceTaxRules WHERE Code='PLATFORM_COMMISSION_STANDARD_18')
    INSERT dbo.FinanceTaxRules
    (Id, Code, SupplyType, TaxOperatingMode, TaxRatePercent, CgstRatePercent, SgstRatePercent, IgstRatePercent, IsDefault, Priority, Description, EffectiveFromUtc, IsActive)
    VALUES
    (NEWID(), 'PLATFORM_COMMISSION_STANDARD_18', 3, NULL, 18, 9, 9, 18, 1, 100,
     'Product default: HealthApp commission at 18%; exact SAC/treatment requires validation.', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FinanceTaxRules WHERE Code='PLATFORM_SETUP_STANDARD_18')
    INSERT dbo.FinanceTaxRules
    (Id, Code, SupplyType, TaxOperatingMode, TaxRatePercent, CgstRatePercent, SgstRatePercent, IgstRatePercent, IsDefault, Priority, Description, EffectiveFromUtc, IsActive)
    VALUES
    (NEWID(), 'PLATFORM_SETUP_STANDARD_18', 4, NULL, 18, 9, 9, 18, 1, 100,
     'Product default: HealthApp setup fee at 18%; exact SAC/treatment requires validation.', SYSUTCDATETIME(), 1);

", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
DECLARE @Defaults TABLE
(
    AccountCode nvarchar(30) NOT NULL,
    Name nvarchar(150) NOT NULL,
    AccountType int NOT NULL,
    NormalBalance int NOT NULL
);
INSERT @Defaults(AccountCode,Name,AccountType,NormalBalance) VALUES
('110100','Payment Gateway Clearing',1,1),
('110200','Bank',1,1),
('120100','Customer Receivable',1,1),
('200100','Outlet Payable',2,2),
('210100','Restaurant GST Payable',2,2),
('210110','CGST Payable',2,2),
('210120','SGST Payable',2,2),
('210130','IGST Payable',2,2),
('210140','ECO Section 9(5) Restaurant GST Payable',2,2),
('220100','Refund Payable',2,2),
('220200','Customer Credit',2,2),
('400100','Restaurant Sales',3,2),
('400200','Platform SaaS Revenue',3,2),
('400300','Platform Commission Revenue',3,2),
('400400','Late Skip Fee Revenue',3,2),
('500100','Gateway Charges',4,1),
('500110','Gateway GST Input',1,1);

INSERT dbo.LedgerAccounts(Id,AccountCode,Name,AccountType,NormalBalance,OutletId,IsSystem,IsActive)
SELECT NEWID(),d.AccountCode,d.Name,d.AccountType,d.NormalBalance,NULL,1,1
FROM @Defaults d
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.LedgerAccounts a
    WHERE a.OutletId IS NULL AND a.AccountCode=d.AccountCode
);
", cancellationToken);
        await SeedFinancePolicyAsync(db, cancellationToken);
    }

    private static async Task SeedFinancePolicyAsync(HealthAppDbContext db, CancellationToken cancellationToken)
    {
        const string code = "FINANCE-CALCULATION-POLICY";
        var document = await db.FinancePolicyDocuments.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        if (document is null)
        {
            document = new FinancePolicyDocument
            {
                Id = Guid.NewGuid(),
                Code = code,
                Title = "Finance calculation and accounting policy",
                Description = "Authoritative implementation record for configurable finance logic, tax calculation, payment, settlement, refund and ledger rules.",
                IsActive = true
            };
            db.FinancePolicyDocuments.Add(document);
        }

        if (await db.FinancePolicyDocumentVersions.AnyAsync(
            x => x.FinancePolicyDocumentId == document.Id && x.Version == "1.0",
            cancellationToken))
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var effective = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var sections = new (string Code, string Title, int Order, string Content)[]
        {
            ("governance", "Finance governance and version control", 10, "Finance logic is versioned and effective-dated. Published rules are not edited in place. Material finance changes create a new policy version with change summary, reason, effective date and source-code reference. Historical transactions remain reproducible from captured versions and snapshots."),
            ("four-boundaries", "Four separate finance boundaries", 20, "Tax liability, economic revenue ownership, payment custody and settlement destination are separate concepts. Payment flow or bank destination does not by itself determine tax liability or revenue ownership."),
            ("restaurant-tax-applicability", "Restaurant tax applicability", 30, "Direct outlet supplier mode: a non-GST-registered outlet does not have restaurant GST added to customer meal/package prices. Inclusive pricing is a pricing mode, not a tax trigger. GST-registered outlets may use inclusive or exclusive pricing according to approved configuration. ECO Section 9(5) is evaluated separately."),
            ("inclusive-exclusive", "Inclusive and exclusive calculation", 40, "For applicable exclusive tax rate r: taxable = price and tax = taxable × r / 100. For applicable inclusive tax: taxable = gross × 100 / (100 + r) and tax = gross − taxable. Currency rounding is applied consistently and calculation inputs/results are snapshotted."),
            ("platform-charges", "HealthApp platform charges", 50, "HealthApp setup fees, annual/SaaS fees and platform commissions are separate HealthApp supplies to the outlet. Their tax treatment is independently configured. Product default is 18% pending CA/tax-adviser approval of exact classification and SAC."),
            ("commission", "Platform commission base", 60, "Product default commission base is restaurant taxable value rather than restaurant GST. The contractual commission basis is configurable and must be documented when changed."),
            ("delivery-and-fees", "Delivery and other charges", 70, "Delivery charges, late-skip fees, gateway fees and their taxes are separate financial components and must be explicitly configured."),
            ("payment-settlement", "Payment and settlement", 80, "Payment transactions, allocations, gateway charges, deductions, refunds and settlement adjustments are separate records linked to financial documents."),
            ("refunds", "Refunds and credit notes", 90, "Refunds are immutable financial events linked to original documents. Original issued documents are not rewritten; credit/debit notes are used for document corrections where required."),
            ("ledger", "Double-entry ledger", 100, "Financial events are posted through balanced double-entry journals. Tax components can be linked to journal lines for reconciliation."),
            ("audit-trace", "Required audit trace", 110, "Target trace: business event → tax calculation → financial document → payment allocation → settlement/refund → ledger → reporting. Finance calculations must retain effective configuration/rule version and finance policy version."),
            ("ca-boundary", "CA/accountant approval boundary", 120, "This records product implementation intent, not legal advice. GST registration, composition treatment, Section 9(5), SAC/classification, place of supply, invoicing and return reporting must be reviewed by the business's qualified tax/accounting adviser.")
        };

        var canonical = string.Join("\n---\n",
            sections.OrderBy(x => x.Order).Select(x => $"{x.Code.Trim()}\n{x.Title.Trim()}\n{x.Content.Trim()}"));

        var version = new FinancePolicyDocumentVersion
        {
            Id = Guid.NewGuid(),
            FinancePolicyDocumentId = document.Id,
            Version = "1.0",
            Status = FinancePolicyPublicationStatus.Published,
            EffectiveFromUtc = effective,
            ChangeSummary = "Initial authoritative finance implementation policy.",
            ChangeReason = "Establish versioned finance governance before transaction-level tax and accounting automation.",
            SourceCodeReference = "FIN-Phase-2A-Governance",
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant(),
            CreatedAtUtc = now,
            PublishedAtUtc = now
        };

        db.FinancePolicyDocumentVersions.Add(version);
        foreach (var section in sections)
            db.FinancePolicyDocumentSections.Add(new FinancePolicyDocumentSection
            {
                Id = Guid.NewGuid(),
                FinancePolicyDocumentVersionId = version.Id,
                SectionCode = section.Code,
                Title = section.Title,
                DisplayOrder = section.Order,
                ContentMarkdown = section.Content,
                CreatedAtUtc = now
            });

        document.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
    }
}
