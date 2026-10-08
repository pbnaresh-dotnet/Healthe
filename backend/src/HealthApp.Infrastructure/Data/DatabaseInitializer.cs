using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
namespace HealthApp.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthAppDbContext>();
        // The checked-in demo package is self-contained. A production deployment should
        // replace EnsureCreatedAsync with EF Core MigrateAsync after generating migrations.
        await db.Database.EnsureCreatedAsync(cancellationToken);

        // Outlet groups are a platform-level tenant classification used by Super Admin reports.
        // Reconcile the group table and nullable tenant link for existing databases.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletGroups','U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletGroups
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletGroups PRIMARY KEY,
        Name nvarchar(120) NOT NULL,
        Description nvarchar(500) NOT NULL CONSTRAINT DF_OutletGroups_Description DEFAULT '',
        IsActive bit NOT NULL CONSTRAINT DF_OutletGroups_IsActive DEFAULT 1,
        SortOrder int NOT NULL CONSTRAINT DF_OutletGroups_SortOrder DEFAULT 0
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_OutletGroups_Name' AND object_id=OBJECT_ID('dbo.OutletGroups'))
    CREATE UNIQUE INDEX UX_OutletGroups_Name ON dbo.OutletGroups(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletGroups_IsActive_SortOrder' AND object_id=OBJECT_ID('dbo.OutletGroups'))
    CREATE INDEX IX_OutletGroups_IsActive_SortOrder ON dbo.OutletGroups(IsActive, SortOrder);
IF COL_LENGTH('dbo.Outlets','OutletGroupId') IS NULL
    ALTER TABLE dbo.Outlets ADD OutletGroupId uniqueidentifier NULL;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Outlets_OutletGroupId' AND object_id=OBJECT_ID('dbo.Outlets'))
    CREATE INDEX IX_Outlets_OutletGroupId ON dbo.Outlets(OutletGroupId);
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name='FK_Outlets_OutletGroups_OutletGroupId'
      AND parent_object_id=OBJECT_ID('dbo.Outlets')
)
    ALTER TABLE dbo.Outlets ADD CONSTRAINT FK_Outlets_OutletGroups_OutletGroupId
    FOREIGN KEY(OutletGroupId) REFERENCES dbo.OutletGroups(Id) ON DELETE NO ACTION;
", cancellationToken);

        // ApplicationErrorLogs is persisted independently of the request DbContext so
        // production errors can be inspected by SuperAdmin even when an older database
        // predates the current EF model.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.ApplicationErrorLogs','U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationErrorLogs
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ApplicationErrorLogs PRIMARY KEY,
        OccurredAtUtc datetime2 NOT NULL,
        Environment nvarchar(40) NOT NULL,
        Severity nvarchar(20) NOT NULL,
        ErrorCode nvarchar(160) NOT NULL,
        Activity nvarchar(300) NOT NULL,
        ExceptionType nvarchar(300) NOT NULL,
        Message nvarchar(4000) NOT NULL,
        InnerExceptionMessage nvarchar(4000) NOT NULL,
        StackTrace nvarchar(max) NOT NULL,
        RequestPath nvarchar(500) NOT NULL,
        HttpMethod nvarchar(16) NOT NULL,
        StatusCode int NOT NULL,
        CorrelationId nvarchar(100) NOT NULL,
        TraceId nvarchar(100) NOT NULL,
        ElapsedMilliseconds bigint NOT NULL,
        UserId uniqueidentifier NULL,
        OutletId uniqueidentifier NULL,
        UserRole nvarchar(50) NOT NULL,
        TenantSlug nvarchar(100) NOT NULL,
        TenantHost nvarchar(255) NOT NULL,
        ClientIpAddress nvarchar(64) NOT NULL,
        UserAgent nvarchar(1000) NOT NULL,
        Fingerprint nvarchar(128) NOT NULL,
        IsResolved bit NOT NULL CONSTRAINT DF_ApplicationErrorLogs_IsResolved DEFAULT 0,
        ResolvedAtUtc datetime2 NULL,
        ResolvedByUserId uniqueidentifier NULL,
        ResolutionNotes nvarchar(2000) NOT NULL CONSTRAINT DF_ApplicationErrorLogs_ResolutionNotes DEFAULT ''
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationErrorLogs_OccurredAtUtc' AND object_id=OBJECT_ID('dbo.ApplicationErrorLogs'))
    CREATE INDEX IX_ApplicationErrorLogs_OccurredAtUtc ON dbo.ApplicationErrorLogs(OccurredAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationErrorLogs_OutletId_OccurredAtUtc' AND object_id=OBJECT_ID('dbo.ApplicationErrorLogs'))
    CREATE INDEX IX_ApplicationErrorLogs_OutletId_OccurredAtUtc ON dbo.ApplicationErrorLogs(OutletId, OccurredAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationErrorLogs_IsResolved_OccurredAtUtc' AND object_id=OBJECT_ID('dbo.ApplicationErrorLogs'))
    CREATE INDEX IX_ApplicationErrorLogs_IsResolved_OccurredAtUtc ON dbo.ApplicationErrorLogs(IsResolved, OccurredAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationErrorLogs_StatusCode_OccurredAtUtc' AND object_id=OBJECT_ID('dbo.ApplicationErrorLogs'))
    CREATE INDEX IX_ApplicationErrorLogs_StatusCode_OccurredAtUtc ON dbo.ApplicationErrorLogs(StatusCode, OccurredAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationErrorLogs_Fingerprint' AND object_id=OBJECT_ID('dbo.ApplicationErrorLogs'))
    CREATE INDEX IX_ApplicationErrorLogs_Fingerprint ON dbo.ApplicationErrorLogs(Fingerprint);
", cancellationToken);

        // Recipe nutrition is now derived from the ingredient reference table.
        // Existing databases need the new calculated sugar field before EF reads Recipes.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Recipes','SugarGrams') IS NULL
    ALTER TABLE dbo.Recipes ADD SugarGrams int NOT NULL CONSTRAINT DF_Recipes_SugarGrams DEFAULT 0 WITH VALUES;
", cancellationToken);

        // Add the column in its own batch first. SQL Server compiles a batch
        // before executing it, so referencing MobileNumber later in the same batch
        // can fail when this is an existing database being upgraded.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users','MobileNumber') IS NULL
    ALTER TABLE dbo.Users ADD MobileNumber nvarchar(20) NULL;
", cancellationToken);

        // Tenant-scoped unique mobile number. Keep this in a separate batch so the
        // newly-added column is visible to SQL Server when the index is created.
        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name='IX_Users_MobileNumber' AND object_id=OBJECT_ID('dbo.Users')
)
    CREATE UNIQUE INDEX IX_Users_MobileNumber
    ON dbo.Users(MobileNumber, OutletId)
    WHERE MobileNumber IS NOT NULL AND MobileNumber <> '';
", cancellationToken);


        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletBrandings','U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletBrandings
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletBrandings PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        BrandName nvarchar(200) NOT NULL CONSTRAINT DF_OutletBrandings_BrandName DEFAULT '',
        Tagline nvarchar(300) NOT NULL CONSTRAINT DF_OutletBrandings_Tagline DEFAULT '',
        LogoUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletBrandings_LogoUrl DEFAULT '',
        HeroImageUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletBrandings_HeroImageUrl DEFAULT '',
        FaviconUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletBrandings_FaviconUrl DEFAULT '',
        PrimaryColor nvarchar(20) NOT NULL CONSTRAINT DF_OutletBrandings_PrimaryColor DEFAULT '#14532d',
        SecondaryColor nvarchar(20) NOT NULL CONSTRAINT DF_OutletBrandings_SecondaryColor DEFAULT '#166534',
        HealthHighlights nvarchar(2000) NOT NULL CONSTRAINT DF_OutletBrandings_HealthHighlights DEFAULT '',
        About nvarchar(4000) NOT NULL CONSTRAINT DF_OutletBrandings_About DEFAULT '',
        FooterText nvarchar(1000) NOT NULL CONSTRAINT DF_OutletBrandings_FooterText DEFAULT '',
        UpdatedAtUtc datetime2 NOT NULL CONSTRAINT DF_OutletBrandings_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletBrandings_OutletId' AND object_id=OBJECT_ID('dbo.OutletBrandings'))
    CREATE UNIQUE INDEX IX_OutletBrandings_OutletId ON dbo.OutletBrandings(OutletId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_OutletBrandings_Outlets' AND parent_object_id=OBJECT_ID('dbo.OutletBrandings'))
    ALTER TABLE dbo.OutletBrandings ADD CONSTRAINT FK_OutletBrandings_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
", cancellationToken);

        // Existing databases may still have the pre-SaaS globally-unique email index.
        // Replace it with a global-null index plus a tenant-scoped email index.
        await db.Database.ExecuteSqlRawAsync(@" 
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX IX_Users_Email ON dbo.Users;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email_Global' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_Email_Global ON dbo.Users(Email) WHERE OutletId IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_OutletId_Email' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_OutletId_Email ON dbo.Users(OutletId, Email) WHERE OutletId IS NOT NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Users') AND referenced_object_id = OBJECT_ID('dbo.Outlets')
)
    ALTER TABLE dbo.Users ADD CONSTRAINT FK_Users_Outlets_OutletId FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.OutletSubscriptions') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.OutletSubscriptions ADD CONSTRAINT FK_OutletSubscriptions_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.PlatformTransactions') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.PlatformTransactions ADD CONSTRAINT FK_PlatformTransactions_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.MealPlans') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.MealPlans ADD CONSTRAINT FK_MealPlans_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Recipes') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.Recipes ADD CONSTRAINT FK_Recipes_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.OutletMenuItems') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.OutletMenuItems ADD CONSTRAINT FK_OutletMenuItems_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Subscriptions') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.Subscriptions ADD CONSTRAINT FK_Subscriptions_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Orders') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Deliveries') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.DeliveryRoutes') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.DeliveryRoutes ADD CONSTRAINT FK_DeliveryRoutes_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.OutletDeliveryAreas') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.OutletDeliveryAreas ADD CONSTRAINT FK_OutletDeliveryAreas_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.DeliveryPricingRules') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.DeliveryPricingRules ADD CONSTRAINT FK_DeliveryPricingRules_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.SubscriptionDiscountTiers') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.SubscriptionDiscountTiers ADD CONSTRAINT FK_SubscriptionDiscountTiers_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.DiscountCodes') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.DiscountCodes ADD CONSTRAINT FK_DiscountCodes_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.CustomerAddresses') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.CustomerAddresses ADD CONSTRAINT FK_CustomerAddresses_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.CustomerCreditTransactions') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.CustomerCreditTransactions ADD CONSTRAINT FK_CustomerCreditTransactions_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Subscriptions') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.Subscriptions ADD CONSTRAINT FK_Subscriptions_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Orders') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.PaymentTransactions') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT FK_PaymentTransactions_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.PlatformTransactions') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.PlatformTransactions ADD CONSTRAINT FK_PlatformTransactions_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Deliveries') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.OutletOnboardingApplications') AND referenced_object_id = OBJECT_ID('dbo.Outlets'))
    ALTER TABLE dbo.OutletOnboardingApplications ADD CONSTRAINT FK_OutletOnboardingApplications_Outlets FOREIGN KEY (OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.OutletOnboardingApplications') AND referenced_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.OutletOnboardingApplications ADD CONSTRAINT FK_OutletOnboardingApplications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
", cancellationToken);

        // Payment gateway schema reconciliation.
        // IMPORTANT: SQL Server compiles an entire batch before executing it. Therefore
        // newly-added payment columns must be created in one batch, and indexes/FKs that
        // reference those columns must be created in a separate batch.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.PaymentTransactions','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.PaymentTransactions','OutletId') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD OutletId uniqueidentifier NULL;
    IF COL_LENGTH('dbo.PaymentTransactions','OutletOnboardingApplicationId') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD OutletOnboardingApplicationId uniqueidentifier NULL;
    IF COL_LENGTH('dbo.PaymentTransactions','PaymentType') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD PaymentType nvarchar(50) NOT NULL CONSTRAINT DF_PaymentTransactions_PaymentType_Compat DEFAULT 'CustomerSubscription' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','ProviderOrderId') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD ProviderOrderId nvarchar(80) NOT NULL CONSTRAINT DF_PaymentTransactions_ProviderOrderId_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','PaymentSessionId') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD PaymentSessionId nvarchar(1000) NOT NULL CONSTRAINT DF_PaymentTransactions_PaymentSessionId_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','PaymentMethod') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD PaymentMethod nvarchar(100) NOT NULL CONSTRAINT DF_PaymentTransactions_PaymentMethod_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','ProviderStatus') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD ProviderStatus nvarchar(50) NOT NULL CONSTRAINT DF_PaymentTransactions_ProviderStatus_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','FailureReason') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD FailureReason nvarchar(1000) NOT NULL CONSTRAINT DF_PaymentTransactions_FailureReason_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.PaymentTransactions','GatewayResponseJson') IS NULL
        ALTER TABLE dbo.PaymentTransactions ADD GatewayResponseJson nvarchar(max) NOT NULL CONSTRAINT DF_PaymentTransactions_GatewayResponseJson_Compat DEFAULT '' WITH VALUES;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PaymentTransactions')
          AND name = 'CustomerId'
          AND is_nullable = 0
    )
        ALTER TABLE dbo.PaymentTransactions ALTER COLUMN CustomerId uniqueidentifier NULL;
END;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.PaymentTransactions','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name='UX_PaymentTransactions_ProviderOrderId'
          AND object_id=OBJECT_ID('dbo.PaymentTransactions')
    )
        CREATE UNIQUE INDEX UX_PaymentTransactions_ProviderOrderId
        ON dbo.PaymentTransactions(ProviderOrderId)
        WHERE ProviderOrderId IS NOT NULL AND ProviderOrderId <> '';

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name='IX_PaymentTransactions_OnboardingApplicationId'
          AND object_id=OBJECT_ID('dbo.PaymentTransactions')
    )
        CREATE INDEX IX_PaymentTransactions_OnboardingApplicationId
        ON dbo.PaymentTransactions(OutletOnboardingApplicationId);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name='FK_PaymentTransactions_Outlets'
      AND parent_object_id=OBJECT_ID('dbo.PaymentTransactions')
)
    ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT FK_PaymentTransactions_Outlets
    FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name='FK_PaymentTransactions_Subscriptions'
      AND parent_object_id=OBJECT_ID('dbo.PaymentTransactions')
)
    ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT FK_PaymentTransactions_Subscriptions
    FOREIGN KEY(SubscriptionId) REFERENCES dbo.Subscriptions(Id) ON DELETE NO ACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name='FK_PaymentTransactions_OutletOnboardingApplications'
      AND parent_object_id=OBJECT_ID('dbo.PaymentTransactions')
)
    ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT FK_PaymentTransactions_OutletOnboardingApplications
    FOREIGN KEY(OutletOnboardingApplicationId) REFERENCES dbo.OutletOnboardingApplications(Id) ON DELETE NO ACTION;
", cancellationToken);

        // Add the column in its own SQL batch. SQL Server may compile the whole batch
        // before executing the ALTER TABLE, which makes a same-batch UPDATE reference
        // to the newly-added column fail with "Invalid column name".
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Outlets','DeliveryDays') IS NULL
    ALTER TABLE dbo.Outlets ADD DeliveryDays nvarchar(200) NULL;
IF COL_LENGTH('dbo.Outlets','DeliveryCoverageMode') IS NULL
    ALTER TABLE dbo.Outlets ADD DeliveryCoverageMode int NOT NULL CONSTRAINT DF_Outlets_DeliveryCoverageMode DEFAULT 1;

IF OBJECT_ID('dbo.OutletBrandings','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.OutletBrandings','FontFamily') IS NULL
        ALTER TABLE dbo.OutletBrandings ADD FontFamily nvarchar(40) NOT NULL CONSTRAINT DF_OutletBrandings_FontFamily DEFAULT 'Inter' WITH VALUES;
    IF COL_LENGTH('dbo.OutletBrandings','ThemeStyle') IS NULL
        ALTER TABLE dbo.OutletBrandings ADD ThemeStyle nvarchar(40) NOT NULL CONSTRAINT DF_OutletBrandings_ThemeStyle DEFAULT 'Fresh' WITH VALUES;
    IF COL_LENGTH('dbo.OutletBrandings','ButtonStyle') IS NULL
        ALTER TABLE dbo.OutletBrandings ADD ButtonStyle nvarchar(40) NOT NULL CONSTRAINT DF_OutletBrandings_ButtonStyle DEFAULT 'Rounded' WITH VALUES;
    IF COL_LENGTH('dbo.OutletBrandings','CardStyle') IS NULL
        ALTER TABLE dbo.OutletBrandings ADD CardStyle nvarchar(40) NOT NULL CONSTRAINT DF_OutletBrandings_CardStyle DEFAULT 'Soft' WITH VALUES;
END
", cancellationToken);

        // Customer legal policies live on the outlet because each standalone outlet owns
        // its customer-facing commercial terms and privacy notice.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Outlets','CustomerTermsAndConditions') IS NULL
    ALTER TABLE dbo.Outlets ADD CustomerTermsAndConditions nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','CustomerPrivacyPolicy') IS NULL
    ALTER TABLE dbo.Outlets ADD CustomerPrivacyPolicy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','CancellationRefundPolicy') IS NULL
    ALTER TABLE dbo.Outlets ADD CancellationRefundPolicy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','MealSkipReschedulePolicy') IS NULL
    ALTER TABLE dbo.Outlets ADD MealSkipReschedulePolicy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','DeliveryPolicy') IS NULL
    ALTER TABLE dbo.Outlets ADD DeliveryPolicy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','AllergenDietaryDisclaimer') IS NULL
    ALTER TABLE dbo.Outlets ADD AllergenDietaryDisclaimer nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','PaymentPricingPromotionalTerms') IS NULL
    ALTER TABLE dbo.Outlets ADD PaymentPricingPromotionalTerms nvarchar(max) NULL;
IF COL_LENGTH('dbo.Outlets','LegalVersion') IS NULL
    ALTER TABLE dbo.Outlets ADD LegalVersion nvarchar(40) NULL;
IF COL_LENGTH('dbo.Outlets','LegalEffectiveDateUtc') IS NULL
    ALTER TABLE dbo.Outlets ADD LegalEffectiveDateUtc datetime2 NULL;
IF COL_LENGTH('dbo.Outlets','LegalPoliciesPublished') IS NULL
    ALTER TABLE dbo.Outlets ADD LegalPoliciesPublished bit NOT NULL CONSTRAINT DF_Outlets_LegalPoliciesPublished DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Outlets','CustomPackagePricingMode') IS NULL
    ALTER TABLE dbo.Outlets ADD CustomPackagePricingMode nvarchar(30) NOT NULL CONSTRAINT DF_Outlets_CustomPackagePricingMode DEFAULT 'Calculated' WITH VALUES;
IF COL_LENGTH('dbo.Outlets','ShowPackagePriceToCustomer') IS NULL
    ALTER TABLE dbo.Outlets ADD ShowPackagePriceToCustomer bit NOT NULL CONSTRAINT DF_Outlets_ShowPackagePriceToCustomer DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.Outlets','ShowMealPriceToCustomer') IS NULL
    ALTER TABLE dbo.Outlets ADD ShowMealPriceToCustomer bit NOT NULL CONSTRAINT DF_Outlets_ShowMealPriceToCustomer DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.Outlets','ShowDeliveryFeeToCustomer') IS NULL
    ALTER TABLE dbo.Outlets ADD ShowDeliveryFeeToCustomer bit NOT NULL CONSTRAINT DF_Outlets_ShowDeliveryFeeToCustomer DEFAULT 1 WITH VALUES;
", cancellationToken);

        // Existing databases also need the new immutable legal-history and customer-acceptance tables.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletLegalPolicyVersions','U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletLegalPolicyVersions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletLegalPolicyVersions PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        Version nvarchar(40) NOT NULL,
        CustomerTermsAndConditions nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Terms DEFAULT '',
        CustomerPrivacyPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Privacy DEFAULT '',
        CancellationRefundPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Cancellation DEFAULT '',
        MealSkipReschedulePolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Skip DEFAULT '',
        DeliveryPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Delivery DEFAULT '',
        AllergenDietaryDisclaimer nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Allergen DEFAULT '',
        PaymentPricingPromotionalTerms nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Payment DEFAULT '',
        ContentHash nvarchar(64) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Hash DEFAULT '',
        EffectiveDateUtc datetime2 NOT NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Created DEFAULT SYSUTCDATETIME(),
        PublishedAtUtc datetime2 NULL,
        CreatedByUserId uniqueidentifier NULL,
        IsPublished bit NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Published DEFAULT 0
    );
END;
IF OBJECT_ID('dbo.CustomerLegalAcceptances','U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLegalAcceptances
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerLegalAcceptances PRIMARY KEY,
        CustomerId uniqueidentifier NOT NULL,
        OutletId uniqueidentifier NOT NULL,
        LegalPolicyVersionId uniqueidentifier NOT NULL,
        TermsAccepted bit NOT NULL,
        PrivacyAccepted bit NOT NULL,
        CommercialPoliciesAccepted bit NOT NULL,
        AcceptedAtUtc datetime2 NOT NULL CONSTRAINT DF_CustomerLegalAcceptances_Accepted DEFAULT SYSUTCDATETIME(),
        IpAddress nvarchar(64) NULL,
        UserAgent nvarchar(1000) NULL
    );
END;
", cancellationToken);

        // Production schema reconciliation for legal history. Existing databases may have been
        // created by an earlier release, so never assume a pre-existing legal table has every
        // current column. Add missing columns first, then reconcile indexes/data, then backfill.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletLegalPolicyVersions','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','Version') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD Version nvarchar(40) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Version_Compat DEFAULT '1.0' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','CustomerTermsAndConditions') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD CustomerTermsAndConditions nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Terms_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','CustomerPrivacyPolicy') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD CustomerPrivacyPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Privacy_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','CancellationRefundPolicy') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD CancellationRefundPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Cancellation_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','MealSkipReschedulePolicy') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD MealSkipReschedulePolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Skip_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','DeliveryPolicy') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD DeliveryPolicy nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Delivery_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','AllergenDietaryDisclaimer') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD AllergenDietaryDisclaimer nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Allergen_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','PaymentPricingPromotionalTerms') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD PaymentPricingPromotionalTerms nvarchar(max) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Payment_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','ContentHash') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD ContentHash nvarchar(64) NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Hash_Compat DEFAULT '' WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','EffectiveDateUtc') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD EffectiveDateUtc datetime2 NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Effective_Compat DEFAULT SYSUTCDATETIME() WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','CreatedAtUtc') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Created_Compat DEFAULT SYSUTCDATETIME() WITH VALUES;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','PublishedAtUtc') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD PublishedAtUtc datetime2 NULL;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','CreatedByUserId') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD CreatedByUserId uniqueidentifier NULL;
    IF COL_LENGTH('dbo.OutletLegalPolicyVersions','IsPublished') IS NULL
        ALTER TABLE dbo.OutletLegalPolicyVersions ADD IsPublished bit NOT NULL CONSTRAINT DF_OutletLegalPolicyVersions_Published_Compat DEFAULT 0 WITH VALUES;
END;

IF OBJECT_ID('dbo.CustomerLegalAcceptances','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','TermsAccepted') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD TermsAccepted bit NOT NULL CONSTRAINT DF_CustomerLegalAcceptances_Terms_Compat DEFAULT 0 WITH VALUES;
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','PrivacyAccepted') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD PrivacyAccepted bit NOT NULL CONSTRAINT DF_CustomerLegalAcceptances_Privacy_Compat DEFAULT 0 WITH VALUES;
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','CommercialPoliciesAccepted') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD CommercialPoliciesAccepted bit NOT NULL CONSTRAINT DF_CustomerLegalAcceptances_Commercial_Compat DEFAULT 0 WITH VALUES;
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','AcceptedAtUtc') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD AcceptedAtUtc datetime2 NOT NULL CONSTRAINT DF_CustomerLegalAcceptances_Accepted_Compat DEFAULT SYSUTCDATETIME() WITH VALUES;
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','IpAddress') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD IpAddress nvarchar(64) NULL;
    IF COL_LENGTH('dbo.CustomerLegalAcceptances','UserAgent') IS NULL
        ALTER TABLE dbo.CustomerLegalAcceptances ADD UserAgent nvarchar(1000) NULL;
END
", cancellationToken);

        // Reconcile legal-history uniqueness before any legacy insert. Older partial deployments
        // may have created one or more of these indexes already, so make the operation rerunnable.
        await db.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_OutletLegalPolicyVersions_Current' AND object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    DROP INDEX UX_OutletLegalPolicyVersions_Current ON dbo.OutletLegalPolicyVersions;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_OutletLegalPolicyVersions_Outlet_Version' AND object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    DROP INDEX UX_OutletLegalPolicyVersions_Outlet_Version ON dbo.OutletLegalPolicyVersions;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletLegalPolicyVersions_Outlet_Published' AND object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    DROP INDEX IX_OutletLegalPolicyVersions_Outlet_Published ON dbo.OutletLegalPolicyVersions;

IF EXISTS (SELECT 1 FROM dbo.OutletLegalPolicyVersions)
BEGIN
    WITH ranked AS
    (
        SELECT Id,
               ROW_NUMBER() OVER
               (
                   PARTITION BY OutletId
                   ORDER BY ISNULL(PublishedAtUtc, CreatedAtUtc) DESC, CreatedAtUtc DESC, Id DESC
               ) AS rn
        FROM dbo.OutletLegalPolicyVersions
        WHERE IsPublished = 1
    )
    UPDATE v
    SET IsPublished = 0
    FROM dbo.OutletLegalPolicyVersions v
    INNER JOIN ranked r ON r.Id=v.Id
    WHERE r.rn > 1;

    WITH duplicates AS
    (
        SELECT Id, OutletId, Version,
               ROW_NUMBER() OVER
               (
                   PARTITION BY OutletId, Version
                   ORDER BY ISNULL(PublishedAtUtc, CreatedAtUtc) DESC, CreatedAtUtc DESC, Id DESC
               ) AS rn
        FROM dbo.OutletLegalPolicyVersions
    )
    UPDATE v
    SET Version = N'legacy-' + REPLACE(CONVERT(varchar(36), v.Id),'-','')
    FROM dbo.OutletLegalPolicyVersions v
    INNER JOIN duplicates d ON d.Id=v.Id
    WHERE d.rn > 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_OutletLegalPolicyVersions_Outlet_Version' AND object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    CREATE UNIQUE INDEX UX_OutletLegalPolicyVersions_Outlet_Version ON dbo.OutletLegalPolicyVersions(OutletId, Version);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_CustomerLegalAcceptances_Customer_Outlet_Version' AND object_id=OBJECT_ID('dbo.CustomerLegalAcceptances'))
    CREATE UNIQUE INDEX UX_CustomerLegalAcceptances_Customer_Outlet_Version ON dbo.CustomerLegalAcceptances(CustomerId, OutletId, LegalPolicyVersionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CustomerLegalAcceptances_Customer_Outlet_Date' AND object_id=OBJECT_ID('dbo.CustomerLegalAcceptances'))
    CREATE INDEX IX_CustomerLegalAcceptances_Customer_Outlet_Date ON dbo.CustomerLegalAcceptances(CustomerId, OutletId, AcceptedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_OutletLegalPolicyVersions_Outlets' AND parent_object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    ALTER TABLE dbo.OutletLegalPolicyVersions ADD CONSTRAINT FK_OutletLegalPolicyVersions_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_CustomerLegalAcceptances_Users' AND parent_object_id=OBJECT_ID('dbo.CustomerLegalAcceptances'))
    ALTER TABLE dbo.CustomerLegalAcceptances ADD CONSTRAINT FK_CustomerLegalAcceptances_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_CustomerLegalAcceptances_Outlets' AND parent_object_id=OBJECT_ID('dbo.CustomerLegalAcceptances'))
    ALTER TABLE dbo.CustomerLegalAcceptances ADD CONSTRAINT FK_CustomerLegalAcceptances_Outlets FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_CustomerLegalAcceptances_Versions' AND parent_object_id=OBJECT_ID('dbo.CustomerLegalAcceptances'))
    ALTER TABLE dbo.CustomerLegalAcceptances ADD CONSTRAINT FK_CustomerLegalAcceptances_Versions FOREIGN KEY(LegalPolicyVersionId) REFERENCES dbo.OutletLegalPolicyVersions(Id) ON DELETE NO ACTION;
", cancellationToken);

        // Backward compatibility: if an older release marked outlet policies as published
        // in the mutable Outlet fields, snapshot the complete policy set into immutable version 1.x
        // before production versioned publishing is enforced.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletLegalPolicyVersions','U') IS NOT NULL
BEGIN
    INSERT INTO dbo.OutletLegalPolicyVersions
    (
        Id, OutletId, Version, CustomerTermsAndConditions, CustomerPrivacyPolicy,
        CancellationRefundPolicy, MealSkipReschedulePolicy, DeliveryPolicy,
        AllergenDietaryDisclaimer, PaymentPricingPromotionalTerms, ContentHash,
        EffectiveDateUtc, CreatedAtUtc, PublishedAtUtc, CreatedByUserId, IsPublished
    )
    SELECT
        NEWID(),
        o.Id,
        CASE
            WHEN LEN(LTRIM(RTRIM(COALESCE(o.LegalVersion,'')))) BETWEEN 1 AND 40
                THEN LTRIM(RTRIM(o.LegalVersion))
            WHEN LEN(LTRIM(RTRIM(COALESCE(o.LegalVersion,'')))) = 0
                THEN N'1.0'
            ELSE N'legacy-' + REPLACE(CONVERT(varchar(36), o.Id),'-','')
        END,
        o.CustomerTermsAndConditions,
        o.CustomerPrivacyPolicy,
        o.CancellationRefundPolicy,
        o.MealSkipReschedulePolicy,
        o.DeliveryPolicy,
        o.AllergenDietaryDisclaimer,
        o.PaymentPricingPromotionalTerms,
        CONVERT(varchar(64), HASHBYTES(
            'SHA2_256',
            CONVERT(nvarchar(max),
                CONCAT(
                    o.CustomerTermsAndConditions, NCHAR(10), N'---', NCHAR(10),
                    o.CustomerPrivacyPolicy, NCHAR(10), N'---', NCHAR(10),
                    o.CancellationRefundPolicy, NCHAR(10), N'---', NCHAR(10),
                    o.MealSkipReschedulePolicy, NCHAR(10), N'---', NCHAR(10),
                    o.DeliveryPolicy, NCHAR(10), N'---', NCHAR(10),
                    o.AllergenDietaryDisclaimer, NCHAR(10), N'---', NCHAR(10),
                    o.PaymentPricingPromotionalTerms
                )
            )
        ), 2),
        COALESCE(o.LegalEffectiveDateUtc,SYSUTCDATETIME()),
        SYSUTCDATETIME(),
        COALESCE(o.LegalEffectiveDateUtc,SYSUTCDATETIME()),
        NULL,
        1
    FROM dbo.Outlets o
    CROSS APPLY
    (
        SELECT CASE
            WHEN LEN(LTRIM(RTRIM(COALESCE(o.LegalVersion,'')))) BETWEEN 1 AND 40
                THEN LTRIM(RTRIM(o.LegalVersion))
            WHEN LEN(LTRIM(RTRIM(COALESCE(o.LegalVersion,'')))) = 0
                THEN N'1.0'
            ELSE N'legacy-' + REPLACE(CONVERT(varchar(36), o.Id),'-','')
        END AS TargetVersion
    ) targetVersion
    WHERE o.LegalPoliciesPublished = 1
      AND NULLIF(o.CustomerTermsAndConditions,'') IS NOT NULL
      AND NULLIF(o.CustomerPrivacyPolicy,'') IS NOT NULL
      AND NULLIF(o.CancellationRefundPolicy,'') IS NOT NULL
      AND NULLIF(o.MealSkipReschedulePolicy,'') IS NOT NULL
      AND NULLIF(o.DeliveryPolicy,'') IS NOT NULL
      AND NULLIF(o.AllergenDietaryDisclaimer,'') IS NOT NULL
      AND NULLIF(o.PaymentPricingPromotionalTerms,'') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.OutletLegalPolicyVersions v
          WHERE v.OutletId = o.Id
            AND v.Version = targetVersion.TargetVersion
      )
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.OutletLegalPolicyVersions v
          WHERE v.OutletId = o.Id
            AND v.IsPublished = 1
      );

    UPDATE o
    SET LegalPoliciesPublished = 0
    FROM dbo.Outlets o
    WHERE o.LegalPoliciesPublished = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.OutletLegalPolicyVersions v
          WHERE v.OutletId = o.Id
            AND v.IsPublished = 1
      );
END;
", cancellationToken);

        // Recreate the filtered unique current-version index only after legacy backfill and
        // all duplicate cleanup have completed.
        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_OutletLegalPolicyVersions_Current' AND object_id=OBJECT_ID('dbo.OutletLegalPolicyVersions'))
    CREATE UNIQUE INDEX UX_OutletLegalPolicyVersions_Current ON dbo.OutletLegalPolicyVersions(OutletId) WHERE IsPublished = 1;
", cancellationToken);

        // Existing databases may receive nullable columns first. Populate safe defaults
        // before EF reads them as required string properties.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Outlets','CustomerTermsAndConditions') IS NOT NULL UPDATE dbo.Outlets SET CustomerTermsAndConditions=ISNULL(CustomerTermsAndConditions,'');
IF COL_LENGTH('dbo.Outlets','CustomerPrivacyPolicy') IS NOT NULL UPDATE dbo.Outlets SET CustomerPrivacyPolicy=ISNULL(CustomerPrivacyPolicy,'');
IF COL_LENGTH('dbo.Outlets','CancellationRefundPolicy') IS NOT NULL UPDATE dbo.Outlets SET CancellationRefundPolicy=ISNULL(CancellationRefundPolicy,'');
IF COL_LENGTH('dbo.Outlets','MealSkipReschedulePolicy') IS NOT NULL UPDATE dbo.Outlets SET MealSkipReschedulePolicy=ISNULL(MealSkipReschedulePolicy,'');
IF COL_LENGTH('dbo.Outlets','DeliveryPolicy') IS NOT NULL UPDATE dbo.Outlets SET DeliveryPolicy=ISNULL(DeliveryPolicy,'');
IF COL_LENGTH('dbo.Outlets','AllergenDietaryDisclaimer') IS NOT NULL UPDATE dbo.Outlets SET AllergenDietaryDisclaimer=ISNULL(AllergenDietaryDisclaimer,'');
IF COL_LENGTH('dbo.Outlets','PaymentPricingPromotionalTerms') IS NOT NULL UPDATE dbo.Outlets SET PaymentPricingPromotionalTerms=ISNULL(PaymentPricingPromotionalTerms,'');
IF COL_LENGTH('dbo.Outlets','LegalVersion') IS NOT NULL UPDATE dbo.Outlets SET LegalVersion=ISNULL(NULLIF(LegalVersion,''),'1.0');
", cancellationToken);

        // Run updates only after the ALTER TABLE batch has completed.
        await db.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT 1 FROM dbo.Outlets WHERE Slug='fitfood')
    UPDATE dbo.Outlets
    SET Status=3,
        DeliveryDays=CASE
            WHEN ISNULL(DeliveryDays,'')='' THEN 'Monday,Tuesday,Wednesday,Thursday,Friday,Saturday'
            ELSE DeliveryDays
        END
    WHERE Slug='fitfood';

IF EXISTS (SELECT 1 FROM dbo.Outlets WHERE Slug='abc')
    UPDATE dbo.Outlets
    SET Status=3,
        DeliveryDays=CASE
            WHEN ISNULL(DeliveryDays,'')='' THEN 'Monday,Tuesday,Wednesday,Thursday,Friday,Saturday'
            ELSE DeliveryDays
        END
    WHERE Slug='abc';
", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users','IsDemo') IS NULL
    ALTER TABLE dbo.Users ADD IsDemo bit NOT NULL CONSTRAINT DF_Users_IsDemo DEFAULT 0;
IF COL_LENGTH('dbo.Users','DemoExpiresAtUtc') IS NULL
    ALTER TABLE dbo.Users ADD DemoExpiresAtUtc datetime2 NULL;
", cancellationToken);
        // Development/demo compatibility: normalized catalog tables are created explicitly because
        // EnsureCreatedAsync does not evolve an already-existing database.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.OutletOnboardingApplications','U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletOnboardingApplications(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletOnboardingApplications PRIMARY KEY,
        AccessKeyHash nvarchar(128) NOT NULL,
        Email nvarchar(320) NOT NULL,
        PasswordHash nvarchar(500) NOT NULL,
        AccountFirstName nvarchar(100) NOT NULL CONSTRAINT DF_OutletOnboarding_AccountFirstName DEFAULT '',
        AccountLastName nvarchar(100) NOT NULL CONSTRAINT DF_OutletOnboarding_AccountLastName DEFAULT '',
        SaaSPlanId uniqueidentifier NOT NULL,
        PlanName nvarchar(100) NOT NULL,
        BillingCycle nvarchar(30) NOT NULL CONSTRAINT DF_OutletOnboarding_BillingCycle DEFAULT 'Monthly',
        SubscriptionFee decimal(18,2) NOT NULL CONSTRAINT DF_OutletOnboarding_SubscriptionFee DEFAULT 0,
        SetupFee decimal(18,2) NOT NULL CONSTRAINT DF_OutletOnboarding_SetupFee DEFAULT 5000,
        PaymentStatus nvarchar(30) NOT NULL CONSTRAINT DF_OutletOnboarding_PaymentStatus DEFAULT 'Pending',
        PaymentReference nvarchar(100) NOT NULL,
        Status nvarchar(40) NOT NULL CONSTRAINT DF_OutletOnboarding_Status DEFAULT 'Onboarding',
        BusinessType nvarchar(40) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessType DEFAULT 'Individual',
        OutletName nvarchar(200) NOT NULL CONSTRAINT DF_OutletOnboarding_OutletName DEFAULT '',
        Description nvarchar(2000) NOT NULL CONSTRAINT DF_OutletOnboarding_Description DEFAULT '',
        City nvarchar(100) NOT NULL CONSTRAINT DF_OutletOnboarding_City DEFAULT '',
        State nvarchar(100) NOT NULL CONSTRAINT DF_OutletOnboarding_State DEFAULT '',
        Pincode nvarchar(20) NOT NULL CONSTRAINT DF_OutletOnboarding_Pincode DEFAULT '',
        AddressLine1 nvarchar(500) NOT NULL CONSTRAINT DF_OutletOnboarding_AddressLine1 DEFAULT '',
        AddressLine2 nvarchar(500) NOT NULL CONSTRAINT DF_OutletOnboarding_AddressLine2 DEFAULT '',
        OwnerName nvarchar(200) NOT NULL CONSTRAINT DF_OutletOnboarding_OwnerName DEFAULT '',
        OwnerEmail nvarchar(320) NOT NULL CONSTRAINT DF_OutletOnboarding_OwnerEmail DEFAULT '',
        OwnerPhone nvarchar(40) NOT NULL CONSTRAINT DF_OutletOnboarding_OwnerPhone DEFAULT '',
        AadhaarNumber nvarchar(20) NOT NULL CONSTRAINT DF_OutletOnboarding_AadhaarNumber DEFAULT '',
        AadhaarCardUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_AadhaarCardUrl DEFAULT '',
        AadhaarCardKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_AadhaarCardKey DEFAULT '',
        AadhaarCardFileName nvarchar(255) NOT NULL CONSTRAINT DF_OutletOnboarding_AadhaarCardFileName DEFAULT '',
        BusinessRegistrationUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessRegistrationUrl DEFAULT '',
        BusinessRegistrationKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessRegistrationKey DEFAULT '',
        BusinessRegistrationFileName nvarchar(255) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessRegistrationFileName DEFAULT '',
        BusinessPan nvarchar(20) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessPan DEFAULT '',
        BusinessPanDocumentUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessPanDocumentUrl DEFAULT '',
        BusinessPanDocumentKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessPanDocumentKey DEFAULT '',
        BusinessPanDocumentFileName nvarchar(255) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessPanDocumentFileName DEFAULT '',
        GstNumber nvarchar(30) NOT NULL CONSTRAINT DF_OutletOnboarding_GstNumber DEFAULT '',
        GstCertificateUrl nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_GstCertificateUrl DEFAULT '',
        GstCertificateKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_GstCertificateKey DEFAULT '',
        GstCertificateFileName nvarchar(255) NOT NULL CONSTRAINT DF_OutletOnboarding_GstCertificateFileName DEFAULT '',
        VerificationNotes nvarchar(2000) NOT NULL CONSTRAINT DF_OutletOnboarding_VerificationNotes DEFAULT '',
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_OutletOnboarding_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        SubmittedAtUtc datetime2 NULL,
        VerifiedAtUtc datetime2 NULL,
        OutletId uniqueidentifier NULL,
        UserId uniqueidentifier NULL
    );
    CREATE UNIQUE INDEX IX_OutletOnboarding_PaymentReference ON dbo.OutletOnboardingApplications(PaymentReference);
    CREATE INDEX IX_OutletOnboarding_Status ON dbo.OutletOnboardingApplications(Status);
    CREATE INDEX IX_OutletOnboarding_Email ON dbo.OutletOnboardingApplications(Email);
END;
IF COL_LENGTH('dbo.OutletOnboardingApplications','AadhaarCardKey') IS NULL
    ALTER TABLE dbo.OutletOnboardingApplications ADD AadhaarCardKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_AadhaarCardKey_Compat DEFAULT '';
IF COL_LENGTH('dbo.OutletOnboardingApplications','BusinessRegistrationKey') IS NULL
    ALTER TABLE dbo.OutletOnboardingApplications ADD BusinessRegistrationKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessRegistrationKey_Compat DEFAULT '';
IF COL_LENGTH('dbo.OutletOnboardingApplications','BusinessPanDocumentKey') IS NULL
    ALTER TABLE dbo.OutletOnboardingApplications ADD BusinessPanDocumentKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_BusinessPanDocumentKey_Compat DEFAULT '';
IF COL_LENGTH('dbo.OutletOnboardingApplications','GstCertificateKey') IS NULL
    ALTER TABLE dbo.OutletOnboardingApplications ADD GstCertificateKey nvarchar(1000) NOT NULL CONSTRAINT DF_OutletOnboarding_GstCertificateKey_Compat DEFAULT '';
IF OBJECT_ID('dbo.ServiceCities','U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceCities(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ServiceCities PRIMARY KEY,
        City nvarchar(100) NOT NULL,
        State nvarchar(100) NOT NULL,
        Country nvarchar(100) NOT NULL,
        Latitude float NOT NULL,
        Longitude float NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_ServiceCities_IsEnabled DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_ServiceCities_City ON dbo.ServiceCities(City);
END;
IF COL_LENGTH('dbo.CustomerAddresses','City') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD City nvarchar(100) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','State') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD State nvarchar(100) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','Pincode') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD Pincode nvarchar(20) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','Locality') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD Locality nvarchar(150) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','CityAreaId') IS NOT NULL
    ALTER TABLE dbo.CustomerAddresses ALTER COLUMN CityAreaId uniqueidentifier NULL;
", cancellationToken);

        // Development/demo compatibility: normalized catalog tables are created explicitly because
        // EnsureCreatedAsync does not evolve an already-existing database.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.Ingredients','U') IS NULL
BEGIN
    CREATE TABLE dbo.Ingredients(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Ingredients PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        DefaultUnit nvarchar(20) NOT NULL,
        CaloriesPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_CaloriesPer100g DEFAULT 0,
        ProteinGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_ProteinPer100g DEFAULT 0,
        CarbsGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_CarbsPer100g DEFAULT 0,
        FatGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_FatPer100g DEFAULT 0,
        FiberGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_FiberPer100g DEFAULT 0,
        SugarGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_SugarPer100g DEFAULT 0,
        NutritionSource nvarchar(200) NOT NULL CONSTRAINT DF_Ingredients_NutritionSource DEFAULT '',
        NutritionReferenceId nvarchar(100) NOT NULL CONSTRAINT DF_Ingredients_NutritionReferenceId DEFAULT '',
        IsActive bit NOT NULL CONSTRAINT DF_Ingredients_IsActive DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_Ingredients_Name ON dbo.Ingredients(Name);
END;

-- Existing standalone databases need the nutrition columns added in a separate
-- batch so SQL Server can compile this block against either schema version.
IF COL_LENGTH('dbo.Ingredients','CaloriesPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD CaloriesPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_CaloriesPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','ProteinGramsPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD ProteinGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_ProteinPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','CarbsGramsPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD CarbsGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_CarbsPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','FatGramsPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD FatGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_FatPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','FiberGramsPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD FiberGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_FiberPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','SugarGramsPer100g') IS NULL
    ALTER TABLE dbo.Ingredients ADD SugarGramsPer100g decimal(10,3) NOT NULL CONSTRAINT DF_Ingredients_SugarPer100g_Compat DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','NutritionSource') IS NULL
    ALTER TABLE dbo.Ingredients ADD NutritionSource nvarchar(200) NOT NULL CONSTRAINT DF_Ingredients_NutritionSource_Compat DEFAULT '' WITH VALUES;
IF COL_LENGTH('dbo.Ingredients','NutritionReferenceId') IS NULL
    ALTER TABLE dbo.Ingredients ADD NutritionReferenceId nvarchar(100) NOT NULL CONSTRAINT DF_Ingredients_NutritionReferenceId_Compat DEFAULT '' WITH VALUES;

IF OBJECT_ID('dbo.Allergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.Allergens(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Allergens PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Allergens_IsActive DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_Allergens_Name ON dbo.Allergens(Name);
END;
IF OBJECT_ID('dbo.RecipeIngredients','U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeIngredients(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_RecipeIngredients PRIMARY KEY,
        RecipeId uniqueidentifier NOT NULL,
        IngredientId uniqueidentifier NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        Unit nvarchar(20) NOT NULL,
        CONSTRAINT FK_RecipeIngredients_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeIngredients_Ingredients FOREIGN KEY(IngredientId) REFERENCES dbo.Ingredients(Id)
    );
    CREATE UNIQUE INDEX IX_RecipeIngredients_Recipe_Ingredient ON dbo.RecipeIngredients(RecipeId,IngredientId);
END;
IF OBJECT_ID('dbo.RecipeAllergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAllergens(
        RecipeId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT PK_RecipeAllergens PRIMARY KEY(RecipeId,AllergenId),
        CONSTRAINT FK_RecipeAllergens_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeAllergens_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
END;
IF OBJECT_ID('dbo.IngredientAllergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.IngredientAllergens(
        IngredientId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT PK_IngredientAllergens PRIMARY KEY(IngredientId,AllergenId),
        CONSTRAINT FK_IngredientAllergens_Ingredients FOREIGN KEY(IngredientId) REFERENCES dbo.Ingredients(Id) ON DELETE CASCADE,
        CONSTRAINT FK_IngredientAllergens_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
END;
IF OBJECT_ID('dbo.CustomerAllergies','U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAllergies(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerAllergies PRIMARY KEY,
        CustomerId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT FK_CustomerAllergies_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerAllergies_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
    CREATE UNIQUE INDEX IX_CustomerAllergies_Customer_Allergen ON dbo.CustomerAllergies(CustomerId,AllergenId);
END;
IF OBJECT_ID('dbo.CustomerLikedMeals','U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLikedMeals(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerLikedMeals PRIMARY KEY,
        CustomerId uniqueidentifier NOT NULL,
        RecipeId uniqueidentifier NOT NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_CustomerLikedMeals_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CustomerLikedMeals_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerLikedMeals_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_CustomerLikedMeals_Customer_Recipe ON dbo.CustomerLikedMeals(CustomerId,RecipeId);
    CREATE INDEX IX_CustomerLikedMeals_Recipe ON dbo.CustomerLikedMeals(RecipeId);
END;

IF OBJECT_ID('dbo.DeliveryRoutes','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryRoutes(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DeliveryRoutes PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        DriverId uniqueidentifier NOT NULL,
        DeliveryDate datetime2 NOT NULL,
        MealSlot int NOT NULL CONSTRAINT DF_DeliveryRoutes_MealSlot DEFAULT 1,
        Status int NOT NULL CONSTRAINT DF_DeliveryRoutes_Status DEFAULT 0,
        TotalDistanceKm float NOT NULL CONSTRAINT DF_DeliveryRoutes_Distance DEFAULT 0,
        TotalDurationMinutes float NOT NULL CONSTRAINT DF_DeliveryRoutes_Duration DEFAULT 0,
        RoutingSource nvarchar(50) NOT NULL CONSTRAINT DF_DeliveryRoutes_RoutingSource DEFAULT 'OSRM',
        GeometryJson nvarchar(max) NOT NULL CONSTRAINT DF_DeliveryRoutes_Geometry DEFAULT '[]',
        CreatedAtUtc datetime2 NOT NULL,
        UpdatedAtUtc datetime2 NOT NULL
    );
    CREATE INDEX IX_DeliveryRoutes_Outlet_Date ON dbo.DeliveryRoutes(OutletId,DeliveryDate);
    CREATE INDEX IX_DeliveryRoutes_Driver_Date ON dbo.DeliveryRoutes(DriverId,DeliveryDate);
END;
IF OBJECT_ID('dbo.DeliveryRouteStops','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryRouteStops(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DeliveryRouteStops PRIMARY KEY,
        RouteId uniqueidentifier NOT NULL,
        StopSequence int NOT NULL,
        MealSlot int NOT NULL CONSTRAINT DF_DeliveryRouteStops_MealSlot DEFAULT 1,
        DeliveryAddressId uniqueidentifier NOT NULL,
        CustomerId uniqueidentifier NOT NULL,
        CustomerName nvarchar(200) NOT NULL,
        Address nvarchar(1000) NOT NULL,
        Latitude float NOT NULL,
        Longitude float NOT NULL,
        DeliveryCount int NOT NULL,
        Status int NOT NULL CONSTRAINT DF_DeliveryRouteStops_Status DEFAULT 0,
        CONSTRAINT FK_DeliveryRouteStops_Routes FOREIGN KEY(RouteId) REFERENCES dbo.DeliveryRoutes(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_DeliveryRouteStops_Route_Sequence ON dbo.DeliveryRouteStops(RouteId,StopSequence);
    CREATE INDEX IX_DeliveryRouteStops_Address ON dbo.DeliveryRouteStops(DeliveryAddressId);
END;
IF COL_LENGTH('dbo.DeliveryRoutes','MealSlot') IS NULL
    ALTER TABLE dbo.DeliveryRoutes ADD MealSlot int NOT NULL CONSTRAINT DF_DeliveryRoutes_MealSlot_Compat DEFAULT 1;
IF COL_LENGTH('dbo.DeliveryRouteStops','MealSlot') IS NULL
    ALTER TABLE dbo.DeliveryRouteStops ADD MealSlot int NOT NULL CONSTRAINT DF_DeliveryRouteStops_MealSlot_Compat DEFAULT 1;
IF COL_LENGTH('dbo.Deliveries','RouteId') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Deliveries','RouteStopId') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteStopId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Deliveries','RouteSequence') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteSequence int NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Deliveries_RouteId' AND object_id=OBJECT_ID('dbo.Deliveries'))
    CREATE INDEX IX_Deliveries_RouteId ON dbo.Deliveries(RouteId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Deliveries_RouteStopId' AND object_id=OBJECT_ID('dbo.Deliveries'))
    CREATE INDEX IX_Deliveries_RouteStopId ON dbo.Deliveries(RouteStopId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.Deliveries') AND referenced_object_id=OBJECT_ID('dbo.DeliveryRoutes'))
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_Routes FOREIGN KEY(RouteId) REFERENCES dbo.DeliveryRoutes(Id) ON DELETE SET NULL;
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name='FK_Deliveries_RouteStops'
      AND parent_object_id=OBJECT_ID('dbo.Deliveries')
      AND referenced_object_id=OBJECT_ID('dbo.DeliveryRouteStops')
      AND delete_referential_action_desc <> 'NO_ACTION'
)
    ALTER TABLE dbo.Deliveries DROP CONSTRAINT FK_Deliveries_RouteStops;
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id=OBJECT_ID('dbo.Deliveries')
      AND referenced_object_id=OBJECT_ID('dbo.DeliveryRouteStops')
)
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_RouteStops FOREIGN KEY(RouteStopId) REFERENCES dbo.DeliveryRouteStops(Id) ON DELETE NO ACTION;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRoutes') AND name='TotalDistanceKm' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRoutes ALTER COLUMN TotalDistanceKm float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRoutes') AND name='TotalDurationMinutes' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRoutes ALTER COLUMN TotalDurationMinutes float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRouteStops') AND name='Latitude' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRouteStops ALTER COLUMN Latitude float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRouteStops') AND name='Longitude' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRouteStops ALTER COLUMN Longitude float NOT NULL;
IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NULL
    ALTER TABLE dbo.Subscriptions ADD DeliveryCity nvarchar(100) NULL;

IF OBJECT_ID('dbo.OutletDomains', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutletDomains
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_OutletDomains PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        Hostname nvarchar(253) NOT NULL,
        VerificationToken nvarchar(128) NOT NULL CONSTRAINT DF_OutletDomains_VerificationToken DEFAULT '',
        VerificationRecordName nvarchar(253) NOT NULL CONSTRAINT DF_OutletDomains_VerificationRecordName DEFAULT '',
        Status int NOT NULL CONSTRAINT DF_OutletDomains_Status DEFAULT 0,
        IsPrimary bit NOT NULL CONSTRAINT DF_OutletDomains_IsPrimary DEFAULT 0,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_OutletDomains_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        VerifiedAtUtc datetime2 NULL
    );
END;

IF COL_LENGTH('dbo.OutletDomains','VerificationToken') IS NULL
    ALTER TABLE dbo.OutletDomains ADD VerificationToken nvarchar(128) NOT NULL CONSTRAINT DF_OutletDomains_VerificationToken_Compat DEFAULT '';
IF COL_LENGTH('dbo.OutletDomains','VerificationRecordName') IS NULL
    ALTER TABLE dbo.OutletDomains ADD VerificationRecordName nvarchar(253) NOT NULL CONSTRAINT DF_OutletDomains_VerificationRecordName_Compat DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletDomains_Hostname' AND object_id=OBJECT_ID('dbo.OutletDomains'))
    CREATE UNIQUE INDEX IX_OutletDomains_Hostname ON dbo.OutletDomains(Hostname);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OutletDomains_OutletId_Status' AND object_id=OBJECT_ID('dbo.OutletDomains'))
    CREATE INDEX IX_OutletDomains_OutletId_Status ON dbo.OutletDomains(OutletId, Status);

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name='FK_OutletDomains_Outlets'
      AND parent_object_id=OBJECT_ID('dbo.OutletDomains')
)
    ALTER TABLE dbo.OutletDomains
        ADD CONSTRAINT FK_OutletDomains_Outlets
        FOREIGN KEY(OutletId) REFERENCES dbo.Outlets(Id) ON DELETE NO ACTION;

IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NULL
    ALTER TABLE dbo.Outlets ADD HeroImageUrl nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Outlets','HealthHighlights') IS NULL
    ALTER TABLE dbo.Outlets ADD HealthHighlights nvarchar(2000) NULL;
IF COL_LENGTH('dbo.Outlets','Rating') IS NULL
    ALTER TABLE dbo.Outlets ADD Rating float NOT NULL CONSTRAINT DF_Outlets_Rating DEFAULT 4.8;
IF COL_LENGTH('dbo.Outlets','ReviewCount') IS NULL
    ALTER TABLE dbo.Outlets ADD ReviewCount int NOT NULL CONSTRAINT DF_Outlets_ReviewCount DEFAULT 0;
IF COL_LENGTH('dbo.Outlets','About') IS NULL
    ALTER TABLE dbo.Outlets ADD About nvarchar(2000) NULL;
IF COL_LENGTH('dbo.Outlets','RestaurantGstRate') IS NULL
    ALTER TABLE dbo.Outlets ADD RestaurantGstRate decimal(9,4) NOT NULL CONSTRAINT DF_Outlets_RestaurantGstRate DEFAULT 5;
IF COL_LENGTH('dbo.Outlets','RestaurantGstMode') IS NULL
    ALTER TABLE dbo.Outlets ADD RestaurantGstMode int NOT NULL CONSTRAINT DF_Outlets_RestaurantGstMode DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','RestaurantGstMode') IS NULL
    ALTER TABLE dbo.Subscriptions ADD RestaurantGstMode int NOT NULL CONSTRAINT DF_Subscriptions_RestaurantGstMode DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','RestaurantTaxableAmount') IS NULL
    ALTER TABLE dbo.Subscriptions ADD RestaurantTaxableAmount decimal(18,2) NOT NULL CONSTRAINT DF_Subscriptions_RestaurantTaxableAmount DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','PackageStatus') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PackageStatus nvarchar(40) NOT NULL CONSTRAINT DF_Subscriptions_PackageStatus DEFAULT 'Active';
IF COL_LENGTH('dbo.Subscriptions','IsOutletCreated') IS NULL
    ALTER TABLE dbo.Subscriptions ADD IsOutletCreated bit NOT NULL CONSTRAINT DF_Subscriptions_IsOutletCreated DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','CreatedByOutletUserId') IS NULL
    ALTER TABLE dbo.Subscriptions ADD CreatedByOutletUserId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Subscriptions','OutletDiscountType') IS NULL
    ALTER TABLE dbo.Subscriptions ADD OutletDiscountType int NOT NULL CONSTRAINT DF_Subscriptions_OutletDiscountType DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','OutletDiscountValue') IS NULL
    ALTER TABLE dbo.Subscriptions ADD OutletDiscountValue decimal(18,2) NOT NULL CONSTRAINT DF_Subscriptions_OutletDiscountValue DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions','OutletDiscountReason') IS NULL
    ALTER TABLE dbo.Subscriptions ADD OutletDiscountReason nvarchar(500) NOT NULL CONSTRAINT DF_Subscriptions_OutletDiscountReason DEFAULT '';
IF COL_LENGTH('dbo.Subscriptions','PaymentMethod') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PaymentMethod nvarchar(40) NOT NULL CONSTRAINT DF_Subscriptions_PaymentMethod DEFAULT 'Online';
IF COL_LENGTH('dbo.Subscriptions','PaidByUserId') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PaidByUserId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Subscriptions','PaidAtUtc') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PaidAtUtc datetime2 NULL;
IF COL_LENGTH('dbo.Subscriptions','AcceptedAtUtc') IS NULL
    ALTER TABLE dbo.Subscriptions ADD AcceptedAtUtc datetime2 NULL;
IF COL_LENGTH('dbo.Subscriptions','SentAtUtc') IS NULL
    ALTER TABLE dbo.Subscriptions ADD SentAtUtc datetime2 NULL;
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantGstRate') IS NULL
    ALTER TABLE dbo.OrderFinancialBreakdowns ADD RestaurantGstRate decimal(9,4) NOT NULL CONSTRAINT DF_OrderFinancialBreakdowns_RestaurantGstRate DEFAULT 5;
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantGstMode') IS NULL
    ALTER TABLE dbo.OrderFinancialBreakdowns ADD RestaurantGstMode int NOT NULL CONSTRAINT DF_OrderFinancialBreakdowns_RestaurantGstMode DEFAULT 0;
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantTaxableAmount') IS NULL
    ALTER TABLE dbo.OrderFinancialBreakdowns ADD RestaurantTaxableAmount decimal(18,2) NOT NULL CONSTRAINT DF_OrderFinancialBreakdowns_RestaurantTaxableAmount DEFAULT 0;

IF COL_LENGTH('dbo.CustomerProfiles','Allergies') IS NOT NULL
    ALTER TABLE dbo.CustomerProfiles ALTER COLUMN Allergies nvarchar(max) NULL;

-- Legacy column retained for older databases; normalized RecipeAllergens/IngredientAllergens are authoritative.
IF COL_LENGTH('dbo.Recipes','FiberGrams') IS NULL
    ALTER TABLE dbo.Recipes ADD FiberGrams int NOT NULL CONSTRAINT DF_Recipes_FiberGrams DEFAULT 0;
IF COL_LENGTH('dbo.Recipes','MealType') IS NULL
    ALTER TABLE dbo.Recipes ADD MealType nvarchar(50) NOT NULL CONSTRAINT DF_Recipes_MealType DEFAULT 'Meal' WITH VALUES;
IF COL_LENGTH('dbo.Recipes','Allergens') IS NOT NULL
    ALTER TABLE dbo.Recipes ALTER COLUMN Allergens nvarchar(max) NULL;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
UPDATE ca
SET City = COALESCE(NULLIF(ca.City, ''), area.City),
    State = COALESCE(NULLIF(ca.State, ''), area.State),
    Pincode = COALESCE(NULLIF(ca.Pincode, ''), area.Pincode),
    Locality = COALESCE(NULLIF(ca.Locality, ''), area.Name)
FROM dbo.CustomerAddresses ca
LEFT JOIN dbo.CityAreas area ON area.Id = ca.CityAreaId
WHERE (ca.City IS NULL OR ca.City = '')
   OR (ca.State IS NULL OR ca.State = '')
   OR (ca.Pincode IS NULL OR ca.Pincode = '')
   OR (ca.Locality IS NULL OR ca.Locality = '');
", cancellationToken);

        // Recipe MealType is added above in a schema batch. Keep its data backfill in a separate
        // batch because SQL Server can compile references to a newly-added column before ALTER TABLE runs.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Recipes','MealType') IS NOT NULL
BEGIN
    UPDATE dbo.Recipes
    SET MealType = 'Meal'
    WHERE MealType IS NULL OR LTRIM(RTRIM(MealType)) = '';
END;
", cancellationToken);

        // Run data updates in separate SQL batches so SQL Server compiles the UPDATE statements
        // only after any newly-added columns exist. This avoids invalid-column and nested-quote failures.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NOT NULL
BEGIN
    UPDATE s
    SET DeliveryCity = COALESCE(NULLIF(s.DeliveryCity, ''), o.City)
    FROM dbo.Subscriptions s
    INNER JOIN dbo.Outlets o ON o.Id = s.OutletId
    WHERE s.DeliveryCity IS NULL OR s.DeliveryCity = '';
END;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NOT NULL
   AND COL_LENGTH('dbo.Outlets','HealthHighlights') IS NOT NULL
BEGIN
    UPDATE dbo.Outlets
    SET HeroImageUrl = COALESCE(NULLIF(HeroImageUrl, ''), 'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=1200&q=85'),
        HealthHighlights = COALESCE(NULLIF(HealthHighlights, ''), 'Grilled,Cold Pressed Oil,High Protein,Exotic Bowls,Fresh Ingredients')
    WHERE Slug = 'fitfood';

    UPDATE dbo.Outlets
    SET HeroImageUrl = COALESCE(NULLIF(HeroImageUrl, ''), 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=85'),
        HealthHighlights = COALESCE(NULLIF(HealthHighlights, ''), 'Fresh Ingredients,Balanced Nutrition,Vegetarian Friendly,High Protein')
    WHERE Slug = 'abc';
END;
", cancellationToken);

        // Existing demo databases can contain NULLs in columns that are now represented by
        // non-nullable C# strings. EF Core materializes those columns with GetString(), which
        // results in SqlNullValueException. Normalize legacy NULLs before any repository query runs.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users','OutletId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Outlets WHERE Slug='fitfood')
BEGIN
    UPDATE u
    SET OutletId = o.Id
    FROM dbo.Users u
    CROSS JOIN dbo.Outlets o
    WHERE u.Email = 'customer@healthapp.test'
      AND u.Role = 0
      AND u.OutletId IS NULL
      AND o.Slug = 'fitfood';
END;

IF COL_LENGTH('dbo.Users','Email') IS NOT NULL UPDATE dbo.Users SET Email = COALESCE(Email,'');
IF COL_LENGTH('dbo.Users','PasswordHash') IS NOT NULL UPDATE dbo.Users SET PasswordHash = COALESCE(PasswordHash,'');
IF COL_LENGTH('dbo.Users','FirstName') IS NOT NULL UPDATE dbo.Users SET FirstName = COALESCE(FirstName,'');
IF COL_LENGTH('dbo.Users','LastName') IS NOT NULL UPDATE dbo.Users SET LastName = COALESCE(LastName,'');

IF COL_LENGTH('dbo.Outlets','Name') IS NOT NULL UPDATE dbo.Outlets SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.Outlets','Slug') IS NOT NULL UPDATE dbo.Outlets SET Slug = COALESCE(Slug,'');
IF COL_LENGTH('dbo.Outlets','Subdomain') IS NOT NULL UPDATE dbo.Outlets SET Subdomain = COALESCE(Subdomain,'');
IF COL_LENGTH('dbo.Outlets','City') IS NOT NULL UPDATE dbo.Outlets SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.Outlets','State') IS NOT NULL UPDATE dbo.Outlets SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.Outlets','Pincode') IS NOT NULL UPDATE dbo.Outlets SET Pincode = COALESCE(Pincode,'');
IF COL_LENGTH('dbo.Outlets','DeliveryDays') IS NOT NULL UPDATE dbo.Outlets SET DeliveryDays = COALESCE(DeliveryDays,'');
IF COL_LENGTH('dbo.Outlets','LogoUrl') IS NOT NULL UPDATE dbo.Outlets SET LogoUrl = COALESCE(LogoUrl,'');
IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NOT NULL UPDATE dbo.Outlets SET HeroImageUrl = COALESCE(HeroImageUrl,'');
IF COL_LENGTH('dbo.Outlets','HealthHighlights') IS NOT NULL UPDATE dbo.Outlets SET HealthHighlights = COALESCE(HealthHighlights,'');
IF COL_LENGTH('dbo.Outlets','PrimaryColor') IS NOT NULL UPDATE dbo.Outlets SET PrimaryColor = COALESCE(PrimaryColor,'#14532d');
IF COL_LENGTH('dbo.Outlets','About') IS NOT NULL UPDATE dbo.Outlets SET About = COALESCE(About,'');

IF COL_LENGTH('dbo.CustomerProfiles','Goal') IS NOT NULL UPDATE dbo.CustomerProfiles SET Goal = COALESCE(Goal,'WeightLoss');
IF COL_LENGTH('dbo.CustomerProfiles','ActivityLevel') IS NOT NULL UPDATE dbo.CustomerProfiles SET ActivityLevel = COALESCE(ActivityLevel,'Moderate');
IF COL_LENGTH('dbo.CustomerProfiles','Diet') IS NOT NULL UPDATE dbo.CustomerProfiles SET Diet = COALESCE(Diet,'');

IF COL_LENGTH('dbo.CustomerAddresses','City') IS NOT NULL UPDATE dbo.CustomerAddresses SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.CustomerAddresses','State') IS NOT NULL UPDATE dbo.CustomerAddresses SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.CustomerAddresses','Pincode') IS NOT NULL UPDATE dbo.CustomerAddresses SET Pincode = COALESCE(Pincode,'');
IF COL_LENGTH('dbo.CustomerAddresses','Locality') IS NOT NULL UPDATE dbo.CustomerAddresses SET Locality = COALESCE(Locality,'');
IF COL_LENGTH('dbo.CustomerAddresses','Label') IS NOT NULL UPDATE dbo.CustomerAddresses SET Label = COALESCE(Label,'');
IF COL_LENGTH('dbo.CustomerAddresses','AddressLine1') IS NOT NULL UPDATE dbo.CustomerAddresses SET AddressLine1 = COALESCE(AddressLine1,'');
IF COL_LENGTH('dbo.CustomerAddresses','AddressLine2') IS NOT NULL UPDATE dbo.CustomerAddresses SET AddressLine2 = COALESCE(AddressLine2,'');
IF COL_LENGTH('dbo.CustomerAddresses','ContactName') IS NOT NULL UPDATE dbo.CustomerAddresses SET ContactName = COALESCE(ContactName,'');
IF COL_LENGTH('dbo.CustomerAddresses','ContactPhone') IS NOT NULL UPDATE dbo.CustomerAddresses SET ContactPhone = COALESCE(ContactPhone,'');

IF COL_LENGTH('dbo.Outlets','RestaurantGstRate') IS NOT NULL UPDATE dbo.Outlets SET RestaurantGstRate = COALESCE(RestaurantGstRate,5);
IF COL_LENGTH('dbo.Outlets','RestaurantGstMode') IS NOT NULL UPDATE dbo.Outlets SET RestaurantGstMode = COALESCE(RestaurantGstMode,0);
IF COL_LENGTH('dbo.Subscriptions','RestaurantGstMode') IS NOT NULL UPDATE dbo.Subscriptions SET RestaurantGstMode = COALESCE(RestaurantGstMode,0);
IF COL_LENGTH('dbo.Subscriptions','RestaurantTaxableAmount') IS NOT NULL UPDATE dbo.Subscriptions SET RestaurantTaxableAmount = CASE WHEN RestaurantTaxableAmount=0 THEN COALESCE(NetMealAmount,0) ELSE RestaurantTaxableAmount END;
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantGstRate') IS NOT NULL UPDATE dbo.OrderFinancialBreakdowns SET RestaurantGstRate = COALESCE(RestaurantGstRate,5);
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantGstMode') IS NOT NULL UPDATE dbo.OrderFinancialBreakdowns SET RestaurantGstMode = COALESCE(RestaurantGstMode,0);
IF COL_LENGTH('dbo.OrderFinancialBreakdowns','RestaurantTaxableAmount') IS NOT NULL UPDATE dbo.OrderFinancialBreakdowns SET RestaurantTaxableAmount = CASE WHEN RestaurantTaxableAmount=0 AND RestaurantGstAmount=0 THEN COALESCE(NetMealAmount,0) ELSE RestaurantTaxableAmount END;

IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NOT NULL UPDATE dbo.Subscriptions SET DeliveryCity = COALESCE(DeliveryCity,'');
IF COL_LENGTH('dbo.Subscriptions','PlanName') IS NOT NULL UPDATE dbo.Subscriptions SET PlanName = COALESCE(PlanName,'');
IF COL_LENGTH('dbo.Subscriptions','Frequency') IS NOT NULL UPDATE dbo.Subscriptions SET Frequency = COALESCE(Frequency,'Weekly');
IF COL_LENGTH('dbo.Subscriptions','DiscountCode') IS NOT NULL UPDATE dbo.Subscriptions SET DiscountCode = COALESCE(DiscountCode,'');

IF COL_LENGTH('dbo.Recipes','Name') IS NOT NULL UPDATE dbo.Recipes SET Name = COALESCE(Name,'');

-- Customer package configuration for existing databases.
IF COL_LENGTH('dbo.MealPlans','IsPreplanned') IS NULL
    ALTER TABLE dbo.MealPlans ADD IsPreplanned bit NOT NULL CONSTRAINT DF_MealPlans_IsPreplanned DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.MealPlans','AvailableCity') IS NULL
    ALTER TABLE dbo.MealPlans ADD AvailableCity nvarchar(100) NOT NULL CONSTRAINT DF_MealPlans_AvailableCity DEFAULT '' WITH VALUES;
IF COL_LENGTH('dbo.MealPlans','DurationDays') IS NULL
    ALTER TABLE dbo.MealPlans ADD DurationDays int NOT NULL CONSTRAINT DF_MealPlans_DurationDays DEFAULT 7 WITH VALUES;
IF COL_LENGTH('dbo.OutletMenuItems','OptionGroup') IS NULL
    ALTER TABLE dbo.OutletMenuItems ADD OptionGroup nvarchar(50) NOT NULL CONSTRAINT DF_OutletMenuItems_OptionGroup DEFAULT 'Main' WITH VALUES;
IF COL_LENGTH('dbo.OutletMenuItems','IsRequired') IS NULL
    ALTER TABLE dbo.OutletMenuItems ADD IsRequired bit NOT NULL CONSTRAINT DF_OutletMenuItems_IsRequired DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.OutletMenuItems','MaxSelections') IS NULL
    ALTER TABLE dbo.OutletMenuItems ADD MaxSelections int NOT NULL CONSTRAINT DF_OutletMenuItems_MaxSelections DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.Subscriptions','IsPreplanned') IS NULL
    ALTER TABLE dbo.Subscriptions ADD IsPreplanned bit NOT NULL CONSTRAINT DF_Subscriptions_IsPreplanned DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Subscriptions','PricingMode') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PricingMode nvarchar(30) NOT NULL CONSTRAINT DF_Subscriptions_PricingMode DEFAULT 'Calculated' WITH VALUES;
IF COL_LENGTH('dbo.Subscriptions','PriceVisibleToCustomer') IS NULL
    ALTER TABLE dbo.Subscriptions ADD PriceVisibleToCustomer bit NOT NULL CONSTRAINT DF_Subscriptions_PriceVisibleToCustomer DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.Subscriptions','DeliveryFeeVisibleToCustomer') IS NULL
    ALTER TABLE dbo.Subscriptions ADD DeliveryFeeVisibleToCustomer bit NOT NULL CONSTRAINT DF_Subscriptions_DeliveryFeeVisibleToCustomer DEFAULT 1 WITH VALUES;
IF COL_LENGTH('dbo.Recipes','Description') IS NOT NULL UPDATE dbo.Recipes SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.Recipes','ImageUrl') IS NOT NULL UPDATE dbo.Recipes SET ImageUrl = COALESCE(ImageUrl,'');
IF COL_LENGTH('dbo.Recipes','Tags') IS NOT NULL UPDATE dbo.Recipes SET Tags = COALESCE(Tags,'');
-- Recipe allergens are normalized in RecipeAllergens; the legacy Recipes.Allergens column is optional and is not updated.

IF COL_LENGTH('dbo.Ingredients','Name') IS NOT NULL UPDATE dbo.Ingredients SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.Ingredients','DefaultUnit') IS NOT NULL UPDATE dbo.Ingredients SET DefaultUnit = COALESCE(DefaultUnit,'g');
IF COL_LENGTH('dbo.Allergens','Name') IS NOT NULL UPDATE dbo.Allergens SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.RecipeIngredients','Unit') IS NOT NULL UPDATE dbo.RecipeIngredients SET Unit = COALESCE(Unit,'g');
IF COL_LENGTH('dbo.MealPlans','Name') IS NOT NULL UPDATE dbo.MealPlans SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.MealPlans','Frequency') IS NOT NULL UPDATE dbo.MealPlans SET Frequency = COALESCE(Frequency,'Weekly');
IF COL_LENGTH('dbo.MealPlans','Currency') IS NOT NULL UPDATE dbo.MealPlans SET Currency = COALESCE(Currency,'INR');
IF COL_LENGTH('dbo.MealPlans','Description') IS NOT NULL UPDATE dbo.MealPlans SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.SaaSPlans','Name') IS NOT NULL UPDATE dbo.SaaSPlans SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.SaaSPlans','Description') IS NOT NULL UPDATE dbo.SaaSPlans SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.OutletSubscriptions','BillingCycle') IS NOT NULL UPDATE dbo.OutletSubscriptions SET BillingCycle = COALESCE(BillingCycle,'Monthly');
IF COL_LENGTH('dbo.OutletSubscriptions','Status') IS NOT NULL UPDATE dbo.OutletSubscriptions SET Status = COALESCE(Status,'Active');

IF COL_LENGTH('dbo.Orders','Address') IS NOT NULL UPDATE dbo.Orders SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.Deliveries','CustomerName') IS NOT NULL UPDATE dbo.Deliveries SET CustomerName = COALESCE(CustomerName,'');
IF COL_LENGTH('dbo.Deliveries','Address') IS NOT NULL UPDATE dbo.Deliveries SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.DeliveryRouteStops','CustomerName') IS NOT NULL UPDATE dbo.DeliveryRouteStops SET CustomerName = COALESCE(CustomerName,'');
IF COL_LENGTH('dbo.DeliveryRouteStops','Address') IS NOT NULL UPDATE dbo.DeliveryRouteStops SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.DeliveryRoutes','RoutingSource') IS NOT NULL UPDATE dbo.DeliveryRoutes SET RoutingSource = COALESCE(RoutingSource,'OSRM');
IF COL_LENGTH('dbo.DeliveryRoutes','GeometryJson') IS NOT NULL UPDATE dbo.DeliveryRoutes SET GeometryJson = COALESCE(GeometryJson,'[]');
IF COL_LENGTH('dbo.MealSelectionHistories','Action') IS NOT NULL UPDATE dbo.MealSelectionHistories SET Action = COALESCE(Action,'');
IF COL_LENGTH('dbo.MealSelectionHistories','Reason') IS NOT NULL UPDATE dbo.MealSelectionHistories SET Reason = COALESCE(Reason,'');
IF COL_LENGTH('dbo.PaymentTransactions','Provider') IS NOT NULL UPDATE dbo.PaymentTransactions SET Provider = COALESCE(Provider,'Mock');
IF COL_LENGTH('dbo.PaymentTransactions','ProviderPaymentId') IS NOT NULL UPDATE dbo.PaymentTransactions SET ProviderPaymentId = COALESCE(ProviderPaymentId,'');
IF COL_LENGTH('dbo.PaymentTransactions','IdempotencyKey') IS NOT NULL UPDATE dbo.PaymentTransactions SET IdempotencyKey = COALESCE(IdempotencyKey,'');
IF COL_LENGTH('dbo.PaymentTransactions','Currency') IS NOT NULL UPDATE dbo.PaymentTransactions SET Currency = COALESCE(Currency,'INR');
IF COL_LENGTH('dbo.PaymentTransactions','Status') IS NOT NULL UPDATE dbo.PaymentTransactions SET Status = COALESCE(Status,'Pending');
IF COL_LENGTH('dbo.DiscountCodes','Code') IS NOT NULL UPDATE dbo.DiscountCodes SET Code = COALESCE(Code,'');
", cancellationToken);

        // ServiceCities can pre-date the current non-nullable C# string model. Normalize
        // legacy NULL text values before DatabaseSeeder queries ServiceCities.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.ServiceCities','City') IS NOT NULL
    UPDATE dbo.ServiceCities SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.ServiceCities','State') IS NOT NULL
    UPDATE dbo.ServiceCities SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.ServiceCities','Country') IS NOT NULL
    UPDATE dbo.ServiceCities SET Country = COALESCE(Country,'India');
", cancellationToken);

        // CityAreas are also queried by the seeder using non-nullable C# strings. Legacy
        // databases can contain NULLs here, which EF materializes with GetString() and fails.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.CityAreas','City') IS NOT NULL
    UPDATE dbo.CityAreas SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.CityAreas','State') IS NOT NULL
    UPDATE dbo.CityAreas SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.CityAreas','Name') IS NOT NULL
    UPDATE dbo.CityAreas SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.CityAreas','Pincode') IS NOT NULL
    UPDATE dbo.CityAreas SET Pincode = COALESCE(Pincode,'');
", cancellationToken);

        // Customer marketing preferences were added after the initial standalone schema.
        // Reconcile these columns before the seeder reads Users through the current EF model.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users','MarketingOptIn') IS NULL
    ALTER TABLE dbo.Users ADD MarketingOptIn bit NOT NULL CONSTRAINT DF_Users_MarketingOptIn DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.Users','MarketingOptInAtUtc') IS NULL
    ALTER TABLE dbo.Users ADD MarketingOptInAtUtc datetime2 NULL;
", cancellationToken);

        // Keep the old text columns harmless for older databases; normalized values are now authoritative.
        await DatabaseSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<HealthApp.Application.Abstractions.IPasswordService>(), cancellationToken);

        // Populate a complete customer-facing legal template for newly seeded/legacy outlets
        // that do not yet have policies. A completely empty policy set is safe to replace with
        // the maintained template; partially edited drafts are completed only where fields are blank
        // and remain unpublished so the outlet owner can review them before publishing.
        await SeedDefaultLegalPoliciesAsync(db, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO dbo.OutletBrandings
(
    Id, OutletId, BrandName, Tagline, LogoUrl, HeroImageUrl, FaviconUrl,
    PrimaryColor, SecondaryColor, HealthHighlights, About, FooterText, UpdatedAtUtc
)
SELECT
    NEWID(), o.Id, o.Name, '', COALESCE(o.LogoUrl,''), COALESCE(o.HeroImageUrl,''), '',
    COALESCE(NULLIF(o.PrimaryColor,''),'#14532d'), '#166534',
    COALESCE(o.HealthHighlights,''), COALESCE(o.About,''), '', SYSUTCDATETIME()
FROM dbo.Outlets o
WHERE NOT EXISTS (SELECT 1 FROM dbo.OutletBrandings b WHERE b.OutletId=o.Id);
", cancellationToken);
    }

    private static async Task SeedDefaultLegalPoliciesAsync(
        HealthAppDbContext db,
        CancellationToken cancellationToken)
    {
        var outlets = await db.Outlets.ToListAsync(cancellationToken);

        foreach (var outlet in outlets)
        {
            var published = await db.OutletLegalPolicyVersions
                .AsNoTracking()
                .AnyAsync(x => x.OutletId == outlet.Id && x.IsPublished, cancellationToken);

            if (published)
                continue;

            var allEmpty =
                string.IsNullOrWhiteSpace(outlet.CustomerTermsAndConditions) &&
                string.IsNullOrWhiteSpace(outlet.CustomerPrivacyPolicy) &&
                string.IsNullOrWhiteSpace(outlet.CancellationRefundPolicy) &&
                string.IsNullOrWhiteSpace(outlet.MealSkipReschedulePolicy) &&
                string.IsNullOrWhiteSpace(outlet.DeliveryPolicy) &&
                string.IsNullOrWhiteSpace(outlet.AllergenDietaryDisclaimer) &&
                string.IsNullOrWhiteSpace(outlet.PaymentPricingPromotionalTerms);

            var name = string.IsNullOrWhiteSpace(outlet.Name) ? "Your Outlet" : outlet.Name.Trim();

            outlet.CustomerTermsAndConditions = string.IsNullOrWhiteSpace(outlet.CustomerTermsAndConditions)
                ? DefaultLegalPolicyTemplates.Terms(name)
                : outlet.CustomerTermsAndConditions;
            outlet.CustomerPrivacyPolicy = string.IsNullOrWhiteSpace(outlet.CustomerPrivacyPolicy)
                ? DefaultLegalPolicyTemplates.Privacy(name)
                : outlet.CustomerPrivacyPolicy;
            outlet.CancellationRefundPolicy = string.IsNullOrWhiteSpace(outlet.CancellationRefundPolicy)
                ? DefaultLegalPolicyTemplates.CancellationRefund(name)
                : outlet.CancellationRefundPolicy;
            outlet.MealSkipReschedulePolicy = string.IsNullOrWhiteSpace(outlet.MealSkipReschedulePolicy)
                ? DefaultLegalPolicyTemplates.SkipReschedule(name)
                : outlet.MealSkipReschedulePolicy;
            outlet.DeliveryPolicy = string.IsNullOrWhiteSpace(outlet.DeliveryPolicy)
                ? DefaultLegalPolicyTemplates.Delivery(name)
                : outlet.DeliveryPolicy;
            outlet.AllergenDietaryDisclaimer = string.IsNullOrWhiteSpace(outlet.AllergenDietaryDisclaimer)
                ? DefaultLegalPolicyTemplates.AllergenDietary(name)
                : outlet.AllergenDietaryDisclaimer;
            outlet.PaymentPricingPromotionalTerms = string.IsNullOrWhiteSpace(outlet.PaymentPricingPromotionalTerms)
                ? DefaultLegalPolicyTemplates.PaymentPricingPromotional(name)
                : outlet.PaymentPricingPromotionalTerms;

            outlet.LegalVersion = string.IsNullOrWhiteSpace(outlet.LegalVersion)
                ? "1.0"
                : outlet.LegalVersion.Trim();

            if (!allEmpty)
            {
                // Existing partial content is a real draft. Keep it editable and let the
                // outlet publish explicitly from Settings after reviewing the completed template.
                outlet.LegalPoliciesPublished = false;
                continue;
            }

            // A brand-new/empty outlet receives a ready-to-publish version 1.0 so customer
            // registration and checkout are not blocked by empty legal placeholders.
            var effectiveDate = DateTime.UtcNow;
            outlet.LegalEffectiveDateUtc = effectiveDate;
            outlet.LegalPoliciesPublished = true;

            var canonical = string.Join("\n---\n",
            [
                outlet.CustomerTermsAndConditions,
                outlet.CustomerPrivacyPolicy,
                outlet.CancellationRefundPolicy,
                outlet.MealSkipReschedulePolicy,
                outlet.DeliveryPolicy,
                outlet.AllergenDietaryDisclaimer,
                outlet.PaymentPricingPromotionalTerms
            ]);

            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();

            db.OutletLegalPolicyVersions.Add(new OutletLegalPolicyVersion
            {
                Id = Guid.NewGuid(),
                OutletId = outlet.Id,
                Version = outlet.LegalVersion,
                CustomerTermsAndConditions = outlet.CustomerTermsAndConditions,
                CustomerPrivacyPolicy = outlet.CustomerPrivacyPolicy,
                CancellationRefundPolicy = outlet.CancellationRefundPolicy,
                MealSkipReschedulePolicy = outlet.MealSkipReschedulePolicy,
                DeliveryPolicy = outlet.DeliveryPolicy,
                AllergenDietaryDisclaimer = outlet.AllergenDietaryDisclaimer,
                PaymentPricingPromotionalTerms = outlet.PaymentPricingPromotionalTerms,
                ContentHash = hash,
                EffectiveDateUtc = effectiveDate,
                CreatedAtUtc = effectiveDate,
                PublishedAtUtc = effectiveDate,
                CreatedByUserId = null,
                IsPublished = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

}
