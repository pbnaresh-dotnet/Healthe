using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;

namespace HealthApp.Application.Services;

public sealed class FinancialDocumentService(
    ISubscriptionRepository subscriptions,
    IOutletRepository outlets,
    IUserRepository users,
    ICustomerAddressRepository addresses,
    IOutletTaxProfileRepository outletTaxProfiles,
    IPlatformTaxProfileRepository platformTaxProfiles,
    IFinanceCalculationSnapshotRepository snapshots,
    IFinanceTaxRuleRepository taxRules,
    IFinancialDocumentRepository documents) : IFinancialDocumentService
{
    private sealed record PartySnapshot(
        string Name,
        string LegalName,
        string TradeName,
        string AddressLine1,
        string AddressLine2,
        string City,
        string State,
        string StateCode,
        string PostalCode,
        string Gstin,
        string Pan);

    public async Task<IReadOnlyList<FinancialDocument>> CreateDraftsForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Subscription not found.");

        var snapshot =
            await snapshots.GetBySourceAsync("CustomerSubscription", subscriptionId)
            ?? await snapshots.GetBySourceAsync("OutletPackageConfirmed", subscriptionId)
            ?? throw new InvalidOperationException("No immutable finance calculation snapshot exists for this subscription.");

        var outlet = await outlets.GetByIdAsync(subscription.OutletId)
            ?? throw new KeyNotFoundException("Outlet not found.");
        var customer = await users.FindByIdAsync(subscription.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var existing = await documents.GetBySourcePrefixAsync("Subscription.", subscriptionId);
        if (existing.Count > 0)
            return existing;

        var outletProfile = await outletTaxProfiles.GetByIdAsync(snapshot.TaxProfileId)
            ?? throw new InvalidOperationException("The outlet tax profile captured by the immutable finance snapshot no longer exists.");

        if (outletProfile.OutletId != subscription.OutletId)
            throw new InvalidOperationException("The finance snapshot tax profile belongs to a different outlet.");

        var platformProfile = snapshot.PlatformTaxProfileId.HasValue
            ? await platformTaxProfiles.GetByIdAsync(snapshot.PlatformTaxProfileId.Value)
            : null;

        if (snapshot.PlatformTaxApplicable && platformProfile is null)
            throw new InvalidOperationException("The finance snapshot requires a platform tax profile, but the captured profile no longer exists.");
        var customerAddress = (await addresses.GetByCustomerAsync(customer.Id))
            .OrderByDescending(x => x.IsDefault)
            .FirstOrDefault();

        var restaurantRule = await taxRules.GetByIdAsync(snapshot.RestaurantTaxRuleId)
            ?? throw new InvalidOperationException("The restaurant tax rule referenced by the snapshot no longer exists.");
        var platformRule = await taxRules.GetByIdAsync(snapshot.PlatformTaxRuleId)
            ?? throw new InvalidOperationException("The platform tax rule referenced by the snapshot no longer exists.");
        var commissionRule = await taxRules.GetByIdAsync(snapshot.CommissionTaxRuleId)
            ?? throw new InvalidOperationException("The commission tax rule referenced by the snapshot no longer exists.");

        var customerParty = new PartySnapshot(
            $"{customer.FirstName} {customer.LastName}".Trim(),
            "",
            "",
            customerAddress?.AddressLine1 ?? "",
            customerAddress?.AddressLine2 ?? "",
            customerAddress?.City ?? "",
            customerAddress?.State ?? "",
            "",
            customerAddress?.Pincode ?? "",
            "",
            "");

        var outletParty = new PartySnapshot(
            outletProfile.TradeName,
            outletProfile.LegalName,
            outletProfile.TradeName,
            outletProfile.AddressLine1,
            outletProfile.AddressLine2,
            outletProfile.City,
            outletProfile.State,
            outletProfile.StateCode,
            outletProfile.PostalCode,
            outletProfile.Gstin,
            outletProfile.Pan);

        var platformParty = platformProfile is null
            ? new PartySnapshot("HealthApp", "", "HealthApp", "", "", "", "", "", "", "", "")
            : new PartySnapshot(
                platformProfile.TradeName,
                platformProfile.LegalName,
                platformProfile.TradeName,
                platformProfile.AddressLine1,
                platformProfile.AddressLine2,
                platformProfile.City,
                platformProfile.State,
                platformProfile.StateCode,
                platformProfile.PostalCode,
                platformProfile.Gstin,
                platformProfile.Pan);

        var result = new List<FinancialDocument>();

        var restaurantIsEco = snapshot.RestaurantTaxOperatingMode == TaxOperatingMode.EcoSection9_5;
        var restaurantIssuer = restaurantIsEco ? platformParty : outletParty;
        var restaurantIssuerType = restaurantIsEco ? FinancePartyType.Platform : FinancePartyType.Outlet;
        var restaurantIssuerId = restaurantIsEco ? platformProfile?.Id ?? Guid.Empty : outlet.Id;

        result.Add(await CreateDraftAsync(
            "Subscription.RestaurantSale",
            subscriptionId,
            subscription.OutletId,
            FinanceSupplyType.RestaurantSale,
            restaurantIssuerType,
            restaurantIssuerId,
            FinancePartyType.Customer,
            customer.Id,
            restaurantIssuer,
            customerParty,
            snapshot.RestaurantTaxableAmount,
            snapshot.RestaurantTaxAmount,
            snapshot.RestaurantTaxApplicable ? snapshot.RestaurantRate : 0m,
            restaurantRule,
            snapshot.FinancePolicyDocumentVersionId,
            snapshot.Id,
            restaurantIsEco));

        if (snapshot.PlatformServiceFee > 0m)
        {
            result.Add(await CreateDraftAsync(
                "Subscription.PlatformService",
                subscriptionId,
                subscription.OutletId,
                FinanceSupplyType.PlatformSaaS,
                FinancePartyType.Platform,
                platformProfile?.Id ?? Guid.Empty,
                FinancePartyType.Customer,
                customer.Id,
                platformParty,
                customerParty,
                snapshot.PlatformServiceFee,
                snapshot.PlatformTaxAmount,
                snapshot.PlatformTaxApplicable ? snapshot.PlatformTaxRate : 0m,
                platformRule,
                snapshot.FinancePolicyDocumentVersionId,
                snapshot.Id,
                false));
        }

        if (snapshot.CommissionAmount > 0m)
        {
            result.Add(await CreateDraftAsync(
                "Subscription.PlatformCommission",
                subscriptionId,
                subscription.OutletId,
                FinanceSupplyType.PlatformCommission,
                FinancePartyType.Platform,
                platformProfile?.Id ?? Guid.Empty,
                FinancePartyType.Outlet,
                outlet.Id,
                platformParty,
                outletParty,
                snapshot.CommissionAmount,
                snapshot.CommissionTaxAmount,
                snapshot.PlatformTaxApplicable ? snapshot.CommissionTaxRate : 0m,
                commissionRule,
                snapshot.FinancePolicyDocumentVersionId,
                snapshot.Id,
                false));
        }

        return result;
    }

    private async Task<FinancialDocument> CreateDraftAsync(
        string sourceType,
        Guid sourceId,
        Guid outletId,
        FinanceSupplyType supplyType,
        FinancePartyType issuerType,
        Guid issuerId,
        FinancePartyType recipientType,
        Guid recipientId,
        PartySnapshot supplier,
        PartySnapshot recipient,
        decimal taxableAmount,
        decimal taxAmount,
        decimal taxRate,
        FinanceTaxRule rule,
        Guid? policyVersionId,
        Guid snapshotId,
        bool isEcoSection9_5)
    {
        var existing = await documents.GetBySourceAsync(sourceType, sourceId);
        if (existing is not null)
            return existing;

        var taxable = Math.Round(Math.Max(0m, taxableAmount), 2);
        var tax = Math.Round(Math.Max(0m, taxAmount), 2);
        var now = DateTime.UtcNow;

        var document = new FinancialDocument
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            DocumentType = FinanceDocumentType.Invoice,
            SupplyType = supplyType,
            Status = FinancialDocumentStatus.Draft,
            IssuerType = issuerType,
            IssuerId = issuerId,
            RecipientType = recipientType,
            RecipientId = recipientId,
            SourceType = sourceType,
            SourceId = sourceId,
            FiscalYearStart = GetFiscalYearStart(now),
            IssueDateUtc = now,
            SupplyDateUtc = now,
            Currency = "INR",
            PlaceOfSupplyState = recipient.State,
            PlaceOfSupplyStateCode = recipient.StateCode,
            IsEcoSection9_5 = isEcoSection9_5,
            FinanceCalculationSnapshotId = snapshotId,
            SupplierLegalName = supplier.LegalName,
            SupplierTradeName = supplier.TradeName,
            SupplierAddressLine1 = supplier.AddressLine1,
            SupplierAddressLine2 = supplier.AddressLine2,
            SupplierCity = supplier.City,
            SupplierState = supplier.State,
            SupplierStateCode = supplier.StateCode,
            SupplierPostalCode = supplier.PostalCode,
            SupplierGstin = supplier.Gstin,
            SupplierPan = supplier.Pan,
            RecipientName = recipient.Name,
            RecipientAddressLine1 = recipient.AddressLine1,
            RecipientAddressLine2 = recipient.AddressLine2,
            RecipientCity = recipient.City,
            RecipientState = recipient.State,
            RecipientStateCode = recipient.StateCode,
            RecipientPostalCode = recipient.PostalCode,
            RecipientGstin = recipient.Gstin,
            RecipientPan = recipient.Pan,
            TaxableValue = taxable,
            TotalTax = tax,
            GrandTotal = Math.Round(taxable + tax, 2),
            Notes = "Draft financial document. Issue only after required tax identity, place-of-supply and invoice validation.",
            CreatedAtUtc = now
        };

        await documents.AddAsync(document);
        await documents.AddLineAsync(new FinancialDocumentLine
        {
            Id = Guid.NewGuid(),
            FinancialDocumentId = document.Id,
            LineNumber = 1,
            ItemType = supplyType.ToString(),
            Description = supplyType switch
            {
                FinanceSupplyType.RestaurantSale => "Restaurant meal/package supply",
                FinanceSupplyType.PlatformSaaS => "HealthApp platform service",
                FinanceSupplyType.PlatformCommission => "HealthApp platform commission",
                _ => supplyType.ToString()
            },
            Quantity = 1m,
            Unit = "service",
            UnitPrice = taxable,
            GrossAmount = taxable,
            TaxableAmount = taxable,
            TaxRatePercent = taxRate,
            TaxAmount = tax,
            IsTaxInclusive = supplyType == FinanceSupplyType.RestaurantSale && taxRate > 0m
        });

        if (tax > 0m)
        {
            var lineId = (await documents.GetBySourceAsync(sourceType, sourceId))?.Id;
            foreach (var component in SplitTax(document, rule))
                await documents.AddTaxComponentAsync(component);
        }

        return document;
    }

    private static IReadOnlyList<FinancialTaxComponent> SplitTax(
        FinancialDocument document,
        FinanceTaxRule rule)
    {
        var baseAmount = document.TaxableValue;
        var tax = document.TotalTax;
        var result = new List<FinancialTaxComponent>();

        if (rule.CgstRatePercent > 0m && rule.SgstRatePercent > 0m)
        {
            var cgst = Math.Round(baseAmount * rule.CgstRatePercent / 100m, 2);
            var sgst = Math.Round(tax - cgst, 2);
            result.Add(NewTax(document, FinancialTaxComponentType.Cgst, rule.CgstRatePercent, cgst, "Configured intra-state split"));
            result.Add(NewTax(document, FinancialTaxComponentType.Sgst, rule.SgstRatePercent, sgst, "Configured intra-state split"));
        }
        else if (rule.IgstRatePercent > 0m)
        {
            result.Add(NewTax(document, FinancialTaxComponentType.Igst, rule.IgstRatePercent, tax, "Configured inter-state split"));
        }

        return result;
    }

    private static FinancialTaxComponent NewTax(
        FinancialDocument document,
        FinancialTaxComponentType type,
        decimal rate,
        decimal amount,
        string jurisdiction)
        => new()
        {
            Id = Guid.NewGuid(),
            FinancialDocumentId = document.Id,
            Component = type,
            Nature = FinancialTaxNature.Output,
            RatePercent = rate,
            TaxableAmount = document.TaxableValue,
            TaxAmount = Math.Round(Math.Max(0m, amount), 2),
            Jurisdiction = jurisdiction,
            IsSection9_5Liability = document.IsEcoSection9_5
        };

    private static int GetFiscalYearStart(DateTime utc)
        => utc.Month >= 4 ? utc.Year : utc.Year - 1;
}
