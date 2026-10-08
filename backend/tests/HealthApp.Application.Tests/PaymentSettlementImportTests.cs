using HealthApp.Application.Abstractions;
using HealthApp.Application.Services;
using HealthApp.Domain.Entities;
using Moq;
using System.Text;
using Xunit;

namespace HealthApp.Application.Tests;

public sealed class PaymentSettlementImportTests
{
    [Fact]
    public async Task ImportCsv_UnmatchedPayment_PersistsOpenException()
    {
        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();

        payments.Setup(x => x.GetByProviderPaymentIdAsync("Cashfree", "CF-MISSING"))
            .ReturnsAsync((PaymentTransaction?)null);
        exceptions.Setup(x => x.GetOpenAsync(
                "Cashfree", "CF-MISSING", "SET-1", "PaymentNotFound"))
            .ReturnsAsync((PaymentSettlementReconciliationException?)null);

        var sut = new PaymentSettlementAccountingService(
            payments.Object, settlements.Object, exceptions.Object);

        const string csv =
            "cf_payment_id,settlement_id,service_charge,service_tax,settlement_amount,utr\n" +
            "CF-MISSING,SET-1,2.00,0.36,97.64,UTR-1\n";

        var result = await sut.ImportCsvAsync(
            "Cashfree",
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            "admin");

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ReconciledRows);
        Assert.Equal(1, result.UnmatchedRows);
        exceptions.Verify(x => x.AddAsync(It.Is<PaymentSettlementReconciliationException>(e =>
            e.Provider == "Cashfree" &&
            e.ProviderPaymentId == "CF-MISSING" &&
            e.ProviderSettlementId == "SET-1" &&
            e.ExceptionType == "PaymentNotFound" &&
            e.Status == "Open")), Times.Once);
    }

    [Fact]
    public async Task ImportCsv_ProviderNetMismatch_PersistsValidationException()
    {
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Provider = "Cashfree",
            ProviderPaymentId = "CF-1",
            ProviderOrderId = "ORDER-1",
            Amount = 100m,
            Currency = "INR",
            Status = "Paid",
            OutletId = Guid.NewGuid()
        };

        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();

        payments.Setup(x => x.GetByProviderPaymentIdAsync("Cashfree", "CF-1"))
            .ReturnsAsync(payment);
        settlements.Setup(x => x.GetByPaymentTransactionAsync(payment.Id))
            .ReturnsAsync((PaymentGatewaySettlement?)null);
        exceptions.Setup(x => x.GetOpenAsync(
                "Cashfree", "CF-1", "SET-1", "ValidationFailure"))
            .ReturnsAsync((PaymentSettlementReconciliationException?)null);

        var sut = new PaymentSettlementAccountingService(
            payments.Object, settlements.Object, exceptions.Object);

        const string csv =
            "cf_payment_id,settlement_id,service_charge,service_tax,settlement_amount,utr\n" +
            "CF-1,SET-1,2.00,0.36,96.00,UTR-1\n";

        var result = await sut.ImportCsvAsync(
            "Cashfree",
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            "admin");

        Assert.Equal(1, result.ExceptionRows);
        exceptions.Verify(x => x.AddAsync(It.Is<PaymentSettlementReconciliationException>(e =>
            e.ExceptionType == "ValidationFailure")), Times.Once);
        settlements.Verify(x => x.AddAsync(It.IsAny<PaymentGatewaySettlement>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsv_DuplicateExistingSettlement_DoesNotCreateAnother()
    {
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Provider = "Cashfree",
            ProviderPaymentId = "CF-1",
            ProviderOrderId = "ORDER-1",
            Amount = 100m,
            Currency = "INR",
            Status = "Paid",
            OutletId = Guid.NewGuid()
        };

        var existing = new PaymentGatewaySettlement
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            Provider = "Cashfree",
            ProviderPaymentId = "CF-1",
            ProviderSettlementId = "SET-1",
            Status = "Reconciled"
        };

        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();

        payments.Setup(x => x.GetByProviderPaymentIdAsync("Cashfree", "CF-1"))
            .ReturnsAsync(payment);
        settlements.Setup(x => x.GetByPaymentTransactionAsync(payment.Id))
            .ReturnsAsync(existing);

        var sut = new PaymentSettlementAccountingService(
            payments.Object, settlements.Object, exceptions.Object);

        const string csv =
            "cf_payment_id,settlement_id,service_charge,service_tax,settlement_amount,utr\n" +
            "CF-1,SET-1,2.00,0.36,97.64,UTR-1\n";

        var result = await sut.ImportCsvAsync(
            "Cashfree",
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            "admin");

        Assert.Equal(1, result.AlreadyReconciledRows);
        settlements.Verify(x => x.AddAsync(It.IsAny<PaymentGatewaySettlement>()), Times.Never);
    }
}