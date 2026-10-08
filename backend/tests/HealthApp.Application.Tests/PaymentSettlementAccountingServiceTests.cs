using HealthApp.Application.Abstractions;
using HealthApp.Application.Services;
using HealthApp.Domain.Entities;
using HealthApp.Shared.DTOs;
using Moq;

namespace HealthApp.Application.Tests;

public sealed class PaymentSettlementAccountingServiceTests
{
    [Fact]
    public async Task RecordSettlement_ComputesNetFromProviderCharges()
    {
        var payment = PaidPayment(100m);
        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();
        payments.Setup(x => x.GetAsync(payment.Id)).ReturnsAsync(payment);
        settlements.Setup(x => x.GetByPaymentTransactionAsync(payment.Id)).ReturnsAsync((PaymentGatewaySettlement?)null);

        var sut = new PaymentSettlementAccountingService(payments.Object, settlements.Object, exceptions.Object);

        var result = await sut.RecordSettlementAsync(new(
            payment.Id, "SET-1", 2m, .36m, 0m, null, "{}", "UTR-1", "admin", 100m, 97.64m));

        Assert.Equal(97.64m, result.NetSettlementAmount);
        Assert.Equal("SET-1", result.ProviderSettlementId);
        settlements.Verify(x => x.AddAsync(It.Is<PaymentGatewaySettlement>(s =>
            s.GrossAmount == 100m &&
            s.GatewayFeeAmount == 2m &&
            s.GatewayFeeTaxAmount == .36m &&
            s.NetSettlementAmount == 97.64m)), Times.Once);
    }

    [Fact]
    public async Task RecordSettlement_RejectsReportedNetMismatch()
    {
        var payment = PaidPayment(100m);
        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();
        payments.Setup(x => x.GetAsync(payment.Id)).ReturnsAsync(payment);
        settlements.Setup(x => x.GetByPaymentTransactionAsync(payment.Id)).ReturnsAsync((PaymentGatewaySettlement?)null);

        var sut = new PaymentSettlementAccountingService(payments.Object, settlements.Object, exceptions.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RecordSettlementAsync(new(
                payment.Id, "SET-1", 2m, .36m, 0m, null, "{}", "UTR-1", "admin", 100m, 96m)));
        settlements.Verify(x => x.AddAsync(It.IsAny<PaymentGatewaySettlement>()), Times.Never);
    }

    [Fact]
    public async Task RecordSettlement_ReusesExistingSettlement()
    {
        var payment = PaidPayment(100m);
        var existing = new PaymentGatewaySettlement { Id = Guid.NewGuid(), PaymentTransactionId = payment.Id, Status = "Reconciled" };
        var payments = new Mock<IPaymentTransactionRepository>();
        var settlements = new Mock<IPaymentGatewaySettlementRepository>();
        var exceptions = new Mock<IPaymentSettlementReconciliationExceptionRepository>();
        payments.Setup(x => x.GetAsync(payment.Id)).ReturnsAsync(payment);
        settlements.Setup(x => x.GetByPaymentTransactionAsync(payment.Id)).ReturnsAsync(existing);

        var sut = new PaymentSettlementAccountingService(payments.Object, settlements.Object, exceptions.Object);

        var result = await sut.RecordSettlementAsync(new(
            payment.Id, "SET-NEW", 0m, 0m, 0m, null, "{}", "", "admin"));

        Assert.Same(existing, result);
        settlements.Verify(x => x.AddAsync(It.IsAny<PaymentGatewaySettlement>()), Times.Never);
    }

    [Fact]
    public async Task RecordSettlement_RejectsUnpaidPayment()
    {
        var payment = PaidPayment(100m);
        payment.Status = "Pending";
        var payments = new Mock<IPaymentTransactionRepository>();
        payments.Setup(x => x.GetAsync(payment.Id)).ReturnsAsync(payment);

        var sut = new PaymentSettlementAccountingService(
            payments.Object,
            Mock.Of<IPaymentGatewaySettlementRepository>(),
            Mock.Of<IPaymentSettlementReconciliationExceptionRepository>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RecordSettlementAsync(new(payment.Id, "SET-1", 0m, 0m, 0m, null, "{}", "", "admin")));
    }

    private static PaymentTransaction PaidPayment(decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        Provider = "Cashfree",
        ProviderPaymentId = "CF-PAY-1",
        ProviderOrderId = "ORDER-1",
        Amount = amount,
        Currency = "INR",
        Status = "Paid",
        OutletId = Guid.NewGuid()
    };
}