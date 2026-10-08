namespace HealthApp.Domain.Enums;

public enum TaxOperatingMode
{
    DirectOutletSupplier = 1,
    EcoSection9_5 = 2
}

public enum FinanceDocumentType
{
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    Receipt = 4
}

public enum FinanceSupplyType
{
    RestaurantSale = 1,
    PlatformSaaS = 2,
    PlatformCommission = 3,
    SetupFee = 4,
    Delivery = 5,
    LateSkipFee = 6,
    OtherPlatformService = 7
}

public enum FinancePartyType
{
    Platform = 1,
    Outlet = 2,
    Customer = 3
}

public enum FinancialTaxComponentType
{
    Cgst = 1,
    Sgst = 2,
    Igst = 3,
    Utgst = 4,
    Cess = 5
}

public enum FinancialTaxNature
{
    Output = 1,
    Input = 2,
    Refund = 3
}

public enum FinancialDocumentStatus
{
    Draft = 1,
    Issued = 2,
    Voided = 3
}

public enum FinanceMerchantOwnerType
{
    Platform = 1,
    Outlet = 2
}

public enum FinanceSettlementLineType
{
    Payment = 1,
    GatewayFee = 2,
    GatewayGst = 3,
    PlatformFee = 4,
    PlatformGst = 5,
    Refund = 6,
    Tax = 7,
    Adjustment = 8
}

public enum RefundStatus
{
    Requested = 1,
    Approved = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public enum LedgerAccountType
{
    Asset = 1,
    Liability = 2,
    Revenue = 3,
    Expense = 4,
    Equity = 5
}

public enum LedgerNormalBalance
{
    Debit = 1,
    Credit = 2
}

public enum LedgerJournalStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}
