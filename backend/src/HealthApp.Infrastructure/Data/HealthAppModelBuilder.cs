using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HealthApp.Infrastructure.Data;

internal static class HealthAppModelBuilder
{
    public static void Configure(ModelBuilder b)
    {
        b.HasDefaultSchema("dbo");
        ConfigureUser(b.Entity<User>());
        ConfigureOutlet(b.Entity<Outlet>());
        ConfigureOutletBranding(b.Entity<OutletBranding>());
        ConfigureOutletDomain(b.Entity<OutletDomain>());
        ConfigureSaaSPlan(b.Entity<SaaSPlan>());
        ConfigureOutletSubscription(b.Entity<OutletSubscription>());
        ConfigureOutletOnboarding(b.Entity<OutletOnboardingApplication>());
        ConfigurePlatformTransaction(b.Entity<PlatformTransaction>());
        ConfigureMealPlan(b.Entity<MealPlan>());
        ConfigureRecipe(b.Entity<Recipe>());
        ConfigureIngredient(b.Entity<Ingredient>());
        ConfigureAllergen(b.Entity<Allergen>());
        ConfigureRecipeIngredient(b.Entity<RecipeIngredient>());
        ConfigureRecipeAllergen(b.Entity<RecipeAllergen>());
        ConfigureIngredientAllergen(b.Entity<IngredientAllergen>());
        ConfigureCustomerAllergy(b.Entity<CustomerAllergy>());
        ConfigureCustomerLikedMeal(b.Entity<CustomerLikedMeal>());
        ConfigureMenu(b.Entity<OutletMenuItem>());
        ConfigureSubscription(b.Entity<Subscription>());
        ConfigureSelection(b.Entity<SubscriptionMealSelection>());
        ConfigureCredit(b.Entity<CustomerCreditTransaction>());
        ConfigureOrder(b.Entity<Order>());
        ConfigureDelivery(b.Entity<Delivery>());
        ConfigureDeliveryRoute(b.Entity<DeliveryRoute>());
        ConfigureDeliveryRouteStop(b.Entity<DeliveryRouteStop>());
        ConfigureCustomerProfile(b.Entity<CustomerProfile>());
        ConfigureServiceCity(b.Entity<ServiceCity>());
        ConfigureCityArea(b.Entity<CityArea>());
        ConfigureOutletDeliveryArea(b.Entity<OutletDeliveryArea>());
        ConfigureDeliveryPricing(b.Entity<DeliveryPricingRule>());
        ConfigureCustomerAddress(b.Entity<CustomerAddress>());
        ConfigureDiscountTier(b.Entity<SubscriptionDiscountTier>());
        ConfigureMealSelectionHistory(b.Entity<MealSelectionHistory>());
        ConfigurePayment(b.Entity<PaymentTransaction>());
        ConfigureDiscountCode(b.Entity<DiscountCode>());
        ConfigureOrderFinancial(b.Entity<OrderFinancialBreakdown>());
        ConfigureOutletLegalPolicyVersion(b.Entity<OutletLegalPolicyVersion>());
        ConfigureCustomerLegalAcceptance(b.Entity<CustomerLegalAcceptance>());
        ConfigureOutletForeignKeys(b);
    }

    private static void ConfigureOutletForeignKeys(ModelBuilder b)
    {
        b.Entity<OutletSubscription>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<PlatformTransaction>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<MealPlan>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Recipe>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<OutletMenuItem>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Subscription>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Order>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Delivery>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DeliveryRoute>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<OutletDeliveryArea>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DeliveryPricingRule>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<SubscriptionDiscountTier>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DiscountCode>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);

        b.Entity<CustomerAddress>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<CustomerCreditTransaction>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Subscription>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Order>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<PaymentTransaction>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<PlatformTransaction>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Delivery>().HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<OutletOnboardingApplication>().HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<OutletOnboardingApplication>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureUser(EntityTypeBuilder<User> e)
    {
        e.ToTable("Users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Email).HasMaxLength(320).IsRequired();
        // Customer identities are tenant-scoped. Keep a single global identity for
        // platform users while allowing the same email in different outlet tenants.
        e.HasIndex(x => x.Email)
            .HasDatabaseName("IX_Users_Email_Global")
            .IsUnique()
            .HasFilter("[OutletId] IS NULL");
        e.HasIndex(x => new { x.OutletId, x.Email })
            .HasDatabaseName("IX_Users_OutletId_Email")
            .IsUnique()
            .HasFilter("[OutletId] IS NOT NULL");
        e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        e.Property(x => x.FirstName).HasMaxLength(100);
        e.Property(x => x.LastName).HasMaxLength(100);
        e.Property(x => x.MobileNumber).HasMaxLength(20);
        e.HasIndex(x => new { x.MobileNumber, x.OutletId }).IsUnique()
            .HasFilter("[MobileNumber] IS NOT NULL AND [MobileNumber] <> ''");
        e.Property(x => x.Role).HasConversion<int>();
        e.HasIndex(x => x.OutletId);
        e.HasIndex(x => x.IsDemo);
        e.Property(x => x.DemoExpiresAtUtc);
        e.HasIndex(x => x.IsDemo);
        e.Property(x => x.DemoExpiresAtUtc);
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureOutlet(EntityTypeBuilder<Outlet> e)
    {
        e.ToTable("Outlets");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        e.Property(x => x.Subdomain).HasMaxLength(100).IsRequired();
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.Pincode).HasMaxLength(20).IsRequired();
        e.Property(x => x.DeliveryDays).HasMaxLength(200);
        e.Property(x => x.CustomerTermsAndConditions).HasColumnType("nvarchar(max)");
        e.Property(x => x.CustomerPrivacyPolicy).HasColumnType("nvarchar(max)");
        e.Property(x => x.CancellationRefundPolicy).HasColumnType("nvarchar(max)");
        e.Property(x => x.MealSkipReschedulePolicy).HasColumnType("nvarchar(max)");
        e.Property(x => x.DeliveryPolicy).HasColumnType("nvarchar(max)");
        e.Property(x => x.AllergenDietaryDisclaimer).HasColumnType("nvarchar(max)");
        e.Property(x => x.PaymentPricingPromotionalTerms).HasColumnType("nvarchar(max)");
        e.Property(x => x.LegalVersion).HasMaxLength(40);
        e.Property(x => x.LegalEffectiveDateUtc);
        e.Property(x => x.LegalPoliciesPublished);
        e.Property(x => x.DeliveryCoverageMode).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.BillingPlan).HasConversion<int>();
        e.Property(x => x.LogoUrl).HasMaxLength(1000);
        e.Property(x => x.HeroImageUrl).HasMaxLength(1000);
        e.Property(x => x.HealthHighlights).HasMaxLength(2000);
        e.Property(x => x.PrimaryColor).HasMaxLength(20);
        e.Property(x => x.RestaurantGstRate).HasPrecision(9,4);
        e.Property(x => x.RestaurantGstMode).HasConversion<int>();
        e.HasIndex(x => x.Slug).IsUnique();
        e.HasIndex(x => x.Subdomain).IsUnique();
    }
    private static void ConfigureOutletLegalPolicyVersion(EntityTypeBuilder<OutletLegalPolicyVersion> e)
    {
        e.ToTable("OutletLegalPolicyVersions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Version).HasMaxLength(40).IsRequired();
        e.Property(x => x.CustomerTermsAndConditions).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.CustomerPrivacyPolicy).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.CancellationRefundPolicy).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.MealSkipReschedulePolicy).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.DeliveryPolicy).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.AllergenDietaryDisclaimer).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.PaymentPricingPromotionalTerms).HasColumnType("nvarchar(max)").IsRequired();
        e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.EffectiveDateUtc).IsRequired();
        e.Property(x => x.CreatedAtUtc).IsRequired();
        e.Property(x => x.PublishedAtUtc);
        e.Property(x => x.CreatedByUserId);
        e.Property(x => x.IsPublished).IsRequired();
        e.HasIndex(x => new { x.OutletId, x.Version }).IsUnique();
        e.HasIndex(x => new { x.OutletId, x.IsPublished });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureCustomerLegalAcceptance(EntityTypeBuilder<CustomerLegalAcceptance> e)
    {
        e.ToTable("CustomerLegalAcceptances");
        e.HasKey(x => x.Id);
        e.Property(x => x.TermsAccepted).IsRequired();
        e.Property(x => x.PrivacyAccepted).IsRequired();
        e.Property(x => x.CommercialPoliciesAccepted).IsRequired();
        e.Property(x => x.AcceptedAtUtc).IsRequired();
        e.Property(x => x.IpAddress).HasMaxLength(64);
        e.Property(x => x.UserAgent).HasMaxLength(1000);
        e.HasIndex(x => new { x.CustomerId, x.OutletId, x.LegalPolicyVersionId }).IsUnique();
        e.HasIndex(x => new { x.CustomerId, x.OutletId, x.AcceptedAtUtc });
        e.HasOne<Outlet>().WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<OutletLegalPolicyVersion>().WithMany().HasForeignKey(x => x.LegalPolicyVersionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureOutletBranding(EntityTypeBuilder<OutletBranding> e)
    {
        e.ToTable("OutletBrandings");
        e.HasKey(x => x.Id);
        e.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Tagline).HasMaxLength(300);
        e.Property(x => x.LogoUrl).HasMaxLength(1000);
        e.Property(x => x.HeroImageUrl).HasMaxLength(1000);
        e.Property(x => x.FaviconUrl).HasMaxLength(1000);
        e.Property(x => x.PrimaryColor).HasMaxLength(20);
        e.Property(x => x.SecondaryColor).HasMaxLength(20);
        e.Property(x => x.HealthHighlights).HasMaxLength(2000);
        e.Property(x => x.About).HasMaxLength(4000);
        e.Property(x => x.FooterText).HasMaxLength(1000);
        e.Property(x => x.FontFamily).HasMaxLength(40);
        e.Property(x => x.ThemeStyle).HasMaxLength(40);
        e.Property(x => x.ButtonStyle).HasMaxLength(40);
        e.Property(x => x.CardStyle).HasMaxLength(40);
        e.HasIndex(x => x.OutletId).IsUnique();
        e.HasOne<Outlet>().WithOne(x => x.Branding).HasForeignKey<OutletBranding>(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureOutletDomain(EntityTypeBuilder<OutletDomain> e)
    {
        e.ToTable("OutletDomains");
        e.HasKey(x => x.Id);
        e.Property(x => x.Hostname).HasMaxLength(253).IsRequired();
        e.Property(x => x.VerificationToken).HasMaxLength(128).IsRequired();
        e.Property(x => x.VerificationRecordName).HasMaxLength(253).IsRequired();
        e.Property(x => x.Status).HasConversion<int>();
        e.HasIndex(x => x.Hostname).IsUnique();
        e.HasIndex(x => new { x.OutletId, x.Status });
        e.HasOne(x => x.Outlet).WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureSaaSPlan(EntityTypeBuilder<SaaSPlan> e)
    {
        e.ToTable("SaaSPlans");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1000);
        e.Property(x => x.MonthlyFee).HasPrecision(18,2);
        e.Property(x => x.AnnualFee).HasPrecision(18,2);
        e.Property(x => x.AdditionalCustomerFee).HasPrecision(18,2);
        e.Property(x => x.CustomerTransactionFeePercent).HasPrecision(9,4);
        e.HasIndex(x => x.Name).IsUnique();
    }
    private static void ConfigureOutletSubscription(EntityTypeBuilder<OutletSubscription> e)
    {
        e.ToTable("OutletSubscriptions");
        e.HasKey(x => x.Id);
        e.Property(x => x.BillingCycle).HasMaxLength(30);
        e.Property(x => x.SubscriptionFee).HasPrecision(18,2);
        e.Property(x => x.SetupFee).HasPrecision(18,2);
        e.Property(x => x.TransactionFeePercent).HasPrecision(9,4);
        e.Property(x => x.Status).HasMaxLength(30);
        e.HasIndex(x => x.OutletId).IsUnique();
        e.HasIndex(x => x.SaaSPlanId);
    }
    private static void ConfigureOutletOnboarding(EntityTypeBuilder<OutletOnboardingApplication> e)
    {
        e.ToTable("OutletOnboardingApplications");
        e.HasKey(x => x.Id);
        e.Property(x => x.AccessKeyHash).HasMaxLength(128).IsRequired();
        e.Property(x => x.Email).HasMaxLength(320).IsRequired();
        e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        e.Property(x => x.AccountFirstName).HasMaxLength(100);
        e.Property(x => x.AccountLastName).HasMaxLength(100);
        e.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
        e.Property(x => x.BillingCycle).HasMaxLength(30);
        e.Property(x => x.SubscriptionFee).HasPrecision(18,2);
        e.Property(x => x.SetupFee).HasPrecision(18,2);
        e.Property(x => x.PaymentStatus).HasMaxLength(30);
        e.Property(x => x.PaymentReference).HasMaxLength(100);
        e.HasIndex(x => x.PaymentReference).IsUnique();
        e.Property(x => x.Status).HasMaxLength(40);
        e.HasIndex(x => x.Status);
        e.Property(x => x.BusinessType).HasMaxLength(40);
        e.Property(x => x.OutletName).HasMaxLength(200);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.City).HasMaxLength(100);
        e.Property(x => x.State).HasMaxLength(100);
        e.Property(x => x.Pincode).HasMaxLength(20);
        e.Property(x => x.AddressLine1).HasMaxLength(500);
        e.Property(x => x.AddressLine2).HasMaxLength(500);
        e.Property(x => x.OwnerName).HasMaxLength(200);
        e.Property(x => x.OwnerEmail).HasMaxLength(320);
        e.Property(x => x.OwnerPhone).HasMaxLength(40);
        e.Property(x => x.AadhaarNumber).HasMaxLength(20);
        e.Property(x => x.AadhaarCardUrl).HasMaxLength(1000);
        e.Property(x => x.AadhaarCardKey).HasMaxLength(1000);
        e.Property(x => x.AadhaarCardFileName).HasMaxLength(255);
        e.Property(x => x.BusinessRegistrationUrl).HasMaxLength(1000);
        e.Property(x => x.BusinessRegistrationKey).HasMaxLength(1000);
        e.Property(x => x.BusinessRegistrationFileName).HasMaxLength(255);
        e.Property(x => x.BusinessPan).HasMaxLength(20);
        e.Property(x => x.BusinessPanDocumentUrl).HasMaxLength(1000);
        e.Property(x => x.BusinessPanDocumentKey).HasMaxLength(1000);
        e.Property(x => x.BusinessPanDocumentFileName).HasMaxLength(255);
        e.Property(x => x.GstNumber).HasMaxLength(30);
        e.Property(x => x.GstCertificateUrl).HasMaxLength(1000);
        e.Property(x => x.GstCertificateKey).HasMaxLength(1000);
        e.Property(x => x.GstCertificateFileName).HasMaxLength(255);
        e.Property(x => x.VerificationNotes).HasMaxLength(2000);
        e.HasIndex(x => x.Email);
    }

    private static void ConfigurePlatformTransaction(EntityTypeBuilder<PlatformTransaction> e)
    {
        e.ToTable("PlatformTransactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Type).HasMaxLength(50).IsRequired();
        e.Property(x => x.ReferenceId).HasMaxLength(200);
        e.Property(x => x.GrossAmount).HasPrecision(18,2);
        e.Property(x => x.PlatformFee).HasPrecision(18,2);
        e.Property(x => x.OutletAmount).HasPrecision(18,2);
        e.Property(x => x.FeePercent).HasPrecision(9,4);
        e.Property(x => x.Currency).HasMaxLength(3);
        e.Property(x => x.Status).HasMaxLength(30);
        e.HasIndex(x => x.ReferenceId).IsUnique().HasFilter("[ReferenceId] IS NOT NULL");
        e.HasIndex(x => new {
            x.OutletId, x.CreatedAt
        });
    }
    private static void ConfigureMealPlan(EntityTypeBuilder<MealPlan> e)
    {
        e.ToTable("MealPlans");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Frequency).HasMaxLength(30);
        e.Property(x => x.Price).HasPrecision(18,2);
        e.Property(x => x.Currency).HasMaxLength(3);
        e.Property(x => x.Description).HasMaxLength(1000);
        e.HasIndex(x => x.OutletId);
    }
    private static void ConfigureRecipe(EntityTypeBuilder<Recipe> e)
    {
        e.ToTable("Recipes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Category).HasConversion<int>();
        e.Property(x => x.PricePerMeal).HasPrecision(18,2);
        e.Property(x => x.LargePricePerMeal).HasPrecision(18,2);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.ImageUrl).HasMaxLength(1000);
        e.Property(x => x.Tags).HasMaxLength(1000);
        e.HasIndex(x => new {
            x.OutletId, x.IsActive
        });
    }
    private static void ConfigureIngredient(EntityTypeBuilder<Ingredient> e)
    {
        e.ToTable("Ingredients");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.DefaultUnit).HasMaxLength(20).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
    }
    private static void ConfigureAllergen(EntityTypeBuilder<Allergen> e)
    {
        e.ToTable("Allergens");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
    }
    private static void ConfigureRecipeIngredient(EntityTypeBuilder<RecipeIngredient> e)
    {
        e.ToTable("RecipeIngredients");
        e.HasKey(x => x.Id);
        e.Property(x => x.Quantity).HasPrecision(18,3);
        e.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        e.HasIndex(x => new {
            x.RecipeId, x.IngredientId
        }).IsUnique();
        e.HasOne(x => x.Recipe).WithMany(x => x.RecipeIngredients).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureRecipeAllergen(EntityTypeBuilder<RecipeAllergen> e)
    {
        e.ToTable("RecipeAllergens");
        e.HasKey(x => new {
            x.RecipeId, x.AllergenId
        });
        e.HasOne(x => x.Recipe).WithMany(x => x.RecipeAllergens).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Allergen).WithMany(x => x.Recipes).HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureIngredientAllergen(EntityTypeBuilder<IngredientAllergen> e)
    {
        e.ToTable("IngredientAllergens");
        e.HasKey(x => new {
            x.IngredientId, x.AllergenId
        });
        e.HasOne(x => x.Ingredient).WithMany(x => x.Allergens).HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Allergen).WithMany(x => x.Ingredients).HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureCustomerAllergy(EntityTypeBuilder<CustomerAllergy> e)
    {
        e.ToTable("CustomerAllergies");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new {
            x.CustomerId, x.AllergenId
        }).IsUnique();
        e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Allergen).WithMany(x => x.Customers).HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureCustomerLikedMeal(EntityTypeBuilder<CustomerLikedMeal> e)
    {
        e.ToTable("CustomerLikedMeals");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.CustomerId, x.RecipeId }).IsUnique();
        e.HasIndex(x => x.RecipeId);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureMenu(EntityTypeBuilder<OutletMenuItem> e)
    {
        e.ToTable("OutletMenuItems");
        e.HasKey(x => x.Id);
        e.Property(x => x.DayOfWeek).HasConversion<int>();
        e.Property(x => x.MealSlot).HasConversion<int>();
        e.HasIndex(x => new {
            x.OutletId, x.DayOfWeek, x.MealSlot, x.RecipeId
        });
        e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureSubscription(EntityTypeBuilder<Subscription> e)
    {
        e.ToTable("Subscriptions");
        e.HasKey(x => x.Id);
        e.Property(x => x.PlanName).HasMaxLength(200);
        e.Property(x => x.DeliveryCity).HasMaxLength(100).IsRequired();
        e.Property(x => x.DeliveryMode).HasConversion<int>();
        e.Property(x => x.Duration).HasConversion<int>();
        e.Property(x => x.GrossMealAmount).HasPrecision(18,2);
        e.Property(x => x.SubscriptionDiscountPercent).HasPrecision(9,4);
        e.Property(x => x.SubscriptionDiscountAmount).HasPrecision(18,2);
        e.Property(x => x.NetMealAmount).HasPrecision(18,2);
        e.Property(x => x.PlatformServiceFee).HasPrecision(18,2);
        e.Property(x => x.PlatformServiceGst).HasPrecision(18,2);
        e.Property(x => x.RestaurantGstRate).HasPrecision(9,4);
        e.Property(x => x.RestaurantGstMode).HasConversion<int>();
        e.Property(x => x.RestaurantTaxableAmount).HasPrecision(18,2);
        e.Property(x => x.RestaurantGstAmount).HasPrecision(18,2);
        e.Property(x => x.LateSkipFee).HasPrecision(18,2);
        e.Property(x => x.Price).HasPrecision(18,2);
        e.Property(x => x.DeliveryFee).HasPrecision(18,2);
        e.Property(x => x.CustomerTransactionFeePercent).HasPrecision(9,4);
        e.Property(x => x.TransactionFee).HasPrecision(18,2);
        e.Property(x => x.TotalCharged).HasPrecision(18,2);
        e.Property(x => x.OutletAmount).HasPrecision(18,2);
        e.Property(x => x.Frequency).HasMaxLength(30);
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.PackageStatus).HasMaxLength(40);
        e.Property(x => x.OutletDiscountType).HasConversion<int>();
        e.Property(x => x.OutletDiscountValue).HasPrecision(18,2);
        e.Property(x => x.OutletDiscountReason).HasMaxLength(500);
        // Explicit SQL Server precision for subscription financial snapshot fields.
        e.Property(x => x.DiscountCodeAmount).HasPrecision(18,2);
        e.Property(x => x.OutletCommissionAmount).HasPrecision(18,2);
        e.Property(x => x.OutletCommissionPercent).HasPrecision(9,4);
        e.Property(x => x.PlatformServiceFeePercent).HasPrecision(9,4);
        e.Property(x => x.PlatformServiceGstRate).HasPrecision(9,4);
        e.Property(x => x.PaymentMethod).HasMaxLength(40);
        e.HasIndex(x => new {
            x.CustomerId, x.Status
        });
        e.HasIndex(x => new {
            x.CustomerId, x.DeliveryCity
        });
        e.HasIndex(x => new {
            x.OutletId, x.Status
        });
        e.HasOne<MealPlan>().WithMany().HasForeignKey(x => x.MealPlanId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureSelection(EntityTypeBuilder<SubscriptionMealSelection> e)
    {
        e.ToTable("SubscriptionMealSelections");
        e.HasKey(x => x.Id);
        e.Property(x => x.MealSlot).HasConversion<int>();
        e.Property(x => x.PortionSize).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.MealPrice).HasPrecision(18,2);
        e.Property(x => x.DeliveryFee).HasPrecision(18,2);
        e.Property(x => x.LateSkipFee).HasPrecision(18,2);
        e.HasIndex(x => new {
            x.SubscriptionId, x.MealDate, x.MealSlot
        });
        e.HasIndex(x => x.RecipeId);
        e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureCredit(EntityTypeBuilder<CustomerCreditTransaction> e)
    {
        e.ToTable("CustomerCreditTransactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Amount).HasPrecision(18,2);
        e.Property(x => x.Type).HasConversion<int>();
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => new {
            x.CustomerId, x.CreatedAt
        });
    }
    private static void ConfigureOrder(EntityTypeBuilder<Order> e)
    {
        e.ToTable("Orders");
        e.HasKey(x => x.Id);
        e.Property(x => x.Total).HasPrecision(18,2);
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.Address).HasMaxLength(1000);
        e.HasIndex(x => new {
            x.CustomerId, x.DeliveryDate
        });
        e.HasIndex(x => new {
            x.OutletId, x.DeliveryDate
        });
    }
    private static void ConfigureDelivery(EntityTypeBuilder<Delivery> e)
    {
        e.ToTable("Deliveries");
        e.HasKey(x => x.Id);
        e.Property(x => x.MealSlot).HasConversion<int>();
        e.Property(x => x.CustomerName).HasMaxLength(200);
        e.Property(x => x.Address).HasMaxLength(1000);
        e.Property(x => x.DeliveryFee).HasPrecision(18,2);
        e.Property(x => x.Status).HasConversion<int>();
        e.HasIndex(x => new {
            x.OutletId, x.ScheduledDate
        });
        e.HasIndex(x => x.DeliveryAddressId);
        e.HasIndex(x => x.RouteId);
        e.HasIndex(x => x.RouteStopId);
        e.HasOne<DeliveryRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.SetNull);
        // RouteStop is intentionally NO ACTION because DeliveryRoute -> DeliveryRouteStops is
        // already cascading. A SET NULL FK here would create two SQL Server cascade paths
        // from DeliveryRoutes to Deliveries (directly and via DeliveryRouteStops).
        e.HasOne<DeliveryRouteStop>().WithMany().HasForeignKey(x => x.RouteStopId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void ConfigureDeliveryRoute(EntityTypeBuilder<DeliveryRoute> e)
    {
        e.ToTable("DeliveryRoutes");
        e.HasKey(x => x.Id);
        e.Property(x => x.MealSlot).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.TotalDistanceKm).HasColumnType("float");
        e.Property(x => x.TotalDurationMinutes).HasColumnType("float");
        e.Property(x => x.RoutingSource).HasMaxLength(50).IsRequired();
        e.Property(x => x.GeometryJson).HasColumnType("nvarchar(max)").IsRequired();
        e.HasIndex(x => new { x.OutletId, x.DeliveryDate });
        e.HasIndex(x => new { x.DriverId, x.DeliveryDate });
    }
    private static void ConfigureDeliveryRouteStop(EntityTypeBuilder<DeliveryRouteStop> e)
    {
        e.ToTable("DeliveryRouteStops");
        e.HasKey(x => x.Id);
        e.Property(x => x.MealSlot).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.CustomerName).HasMaxLength(200);
        e.Property(x => x.Address).HasMaxLength(1000);
        e.Property(x => x.Latitude).HasColumnType("float");
        e.Property(x => x.Longitude).HasColumnType("float");
        e.HasIndex(x => new { x.RouteId, x.StopSequence }).IsUnique();
        e.HasIndex(x => x.DeliveryAddressId);
        e.HasOne<DeliveryRoute>().WithMany(x => x.Stops).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureCustomerProfile(EntityTypeBuilder<CustomerProfile> e)
    {
        e.ToTable("CustomerProfiles");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.CustomerId).IsUnique();
        e.Property(x => x.WeightKg).HasPrecision(8,2);
        e.Property(x => x.HeightCm).HasPrecision(8,2);
        e.Property(x => x.Bmi).HasPrecision(8,2);
        e.Property(x => x.Goal).HasMaxLength(50);
        e.Property(x => x.ActivityLevel).HasMaxLength(50);
        e.Property(x => x.Diet).HasMaxLength(1000);
        e.HasOne<User>().WithOne(x=>x.CustomerProfile).HasForeignKey<CustomerProfile>(x=>x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureServiceCity(EntityTypeBuilder<ServiceCity> e)
    {
        e.ToTable("ServiceCities");
        e.HasKey(x => x.Id);
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.Country).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.City).IsUnique();
    }
    private static void ConfigureCityArea(EntityTypeBuilder<CityArea> e)
    {
        e.ToTable("CityAreas");
        e.HasKey(x => x.Id);
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Pincode).HasMaxLength(20).IsRequired();
        e.HasIndex(x => new {
            x.City, x.Name, x.Pincode
        }).IsUnique();
    }
    private static void ConfigureOutletDeliveryArea(EntityTypeBuilder<OutletDeliveryArea> e)
    {
        e.ToTable("OutletDeliveryAreas");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new {
            x.OutletId, x.CityAreaId
        }).IsUnique();
        e.HasOne<CityArea>().WithMany().HasForeignKey(x => x.CityAreaId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
    }
    private static void ConfigureDeliveryPricing(EntityTypeBuilder<DeliveryPricingRule> e)
    {
        e.ToTable("DeliveryPricingRules");
        e.HasKey(x => x.Id);
        e.Property(x => x.MaxDistanceKm).HasPrecision(8,2);
        e.Property(x => x.Fee).HasPrecision(18,2);
        e.HasIndex(x => new {
            x.OutletId, x.MaxDistanceKm
        }).IsUnique();
    }
    private static void ConfigureCustomerAddress(EntityTypeBuilder<CustomerAddress> e)
    {
        e.ToTable("CustomerAddresses");
        e.HasKey(x => x.Id);
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.State).HasMaxLength(100).IsRequired();
        e.Property(x => x.Pincode).HasMaxLength(20).IsRequired(false);
        e.Property(x => x.Locality).HasMaxLength(150).IsRequired(false);
        e.Property(x => x.Label).HasMaxLength(50).IsRequired();
        e.Property(x => x.AddressLine1).HasMaxLength(300).IsRequired();
        e.Property(x => x.AddressLine2).HasMaxLength(300);
        e.Property(x => x.ContactName).HasMaxLength(150);
        e.Property(x => x.ContactPhone).HasMaxLength(30);
        e.HasIndex(x => new {
            x.CustomerId, x.IsDefault
        });
        e.HasOne<CityArea>().WithMany().HasForeignKey(x => x.CityAreaId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
    }
    private static void ConfigureDiscountTier(EntityTypeBuilder<SubscriptionDiscountTier> e)
    {
        e.ToTable("SubscriptionDiscountTiers");
        e.HasKey(x => x.Id);
        e.Property(x => x.OneWeekPercent).HasPrecision(9,4);
        e.Property(x => x.TwoWeeksPercent).HasPrecision(9,4);
        e.Property(x => x.OneMonthPercent).HasPrecision(9,4);
        e.HasIndex(x => new {
            x.OutletId, x.MinMeals, x.MaxMeals
        }).IsUnique();
    }
    private static void ConfigureMealSelectionHistory(EntityTypeBuilder<MealSelectionHistory> e)
    {
        e.ToTable("MealSelectionHistories");
        e.HasKey(x => x.Id);
        e.Property(x => x.Action).HasMaxLength(50).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(500);
        e.Property(x => x.Amount).HasPrecision(18,2);
        e.HasIndex(x => new {
            x.MealSelectionId, x.OccurredAtUtc
        });
    }
    private static void ConfigurePayment(EntityTypeBuilder<PaymentTransaction> e)
    {
        e.ToTable("PaymentTransactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Provider).HasMaxLength(50);
        e.Property(x => x.ProviderPaymentId).HasMaxLength(200);
        e.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        e.Property(x => x.Amount).HasPrecision(18,2);
        e.Property(x => x.Currency).HasMaxLength(3);
        e.Property(x => x.Status).HasMaxLength(30);
        e.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
    private static void ConfigureDiscountCode(EntityTypeBuilder<DiscountCode> e)
    {
        e.ToTable("DiscountCodes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(50).IsRequired();
        e.Property(x => x.Percent).HasPrecision(9,4);
        e.Property(x => x.MaxAmount).HasPrecision(18,2);
        e.HasIndex(x => new {
            x.OutletId, x.Code
        }).IsUnique();
    }
    private static void ConfigureOrderFinancial(EntityTypeBuilder<OrderFinancialBreakdown> e)
    {
        e.ToTable("OrderFinancialBreakdowns");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.OrderId).IsUnique();
        e.Property(x => x.GrossMealAmount).HasPrecision(18,2);
        e.Property(x => x.DiscountAmount).HasPrecision(18,2);
        e.Property(x => x.NetMealAmount).HasPrecision(18,2);
        e.Property(x => x.DeliveryAmount).HasPrecision(18,2);
        e.Property(x => x.PlatformServiceFee).HasPrecision(18,2);
        e.Property(x => x.PlatformServiceGst).HasPrecision(18,2);
        e.Property(x => x.RestaurantGstRate).HasPrecision(9,4);
        e.Property(x => x.RestaurantGstMode).HasConversion<int>();
        e.Property(x => x.RestaurantTaxableAmount).HasPrecision(18,2);
        e.Property(x => x.RestaurantGstAmount).HasPrecision(18,2);
        e.Property(x => x.LateSkipFee).HasPrecision(18,2);
        e.Property(x => x.CustomerPayable).HasPrecision(18,2);
        e.Property(x => x.OutletCommission).HasPrecision(18,2);
        e.Property(x => x.OutletCommissionGst).HasPrecision(18,2);
        e.Property(x => x.OutletSettlementAmount).HasPrecision(18,2);
        e.Property(x => x.HealthAppRevenue).HasPrecision(18,2);
    }
}
