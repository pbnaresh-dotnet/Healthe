using HealthApp.Application.Abstractions;
using HealthApp.Application.Services;
using HealthApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HealthApp.Application.Tests;

public sealed class PaymentWebhookIdempotencyTests
{
    [Fact]
    public async Task HandleWebhookAsync_DoesNotRefreshOrFulfilWhenAnotherRequestOwnsProcessingLease()
    {
        const string rawBody = "{\"type\":\"PAYMENT_SUCCESS\",\"data\":{\"order\":{\"order_id\":\"order-1\"}}}";
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Provider = "Cashfree",
            ProviderOrderId = "order-1",
            PaymentType = "CustomerSubscription",
            Status = "Pending",
            ProcessingStatus = "WebhookProcessing",
            Amount = 100m,
            Currency = "INR"
        };

        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.VerifyWebhookSignature(
                It.IsAny<IReadOnlyDictionary<string, string>>(), rawBody))
            .Returns(true);
        gateway.Setup(x => x.ParseWebhook(rawBody))
            .Returns(new PaymentGatewayWebhookEvent("order-1", "PAYMENT_SUCCESS"));

        var payments = new Mock<IPaymentTransactionRepository>();
        payments.Setup(x => x.GetByProviderOrderIdAsync("order-1"))
            .ReturnsAsync(payment);
        payments.Setup(x => x.TryClaimPaymentProcessingAsync(
                payment.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        var service = new PaymentService(
            Mock.Of<ICurrentUser>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<ICustomerAddressRepository>(),
            Mock.Of<IOutletOnboardingRepository>(),
            payments.Object,
            Mock.Of<ISubscriptionRepository>(),
            Mock.Of<IOrderRepository>(),
            Mock.Of<IOutletPackageActivationService>(),
            Mock.Of<IOutletRepository>(),
            Mock.Of<IOutletDomainRepository>(),
            Mock.Of<IOptions<TenantDomainSettings>>(),
            gateway.Object,
            Mock.Of<ITransactionalEmailService>(),
            Mock.Of<IConfiguration>());

        var result = await service.HandleWebhookAsync(
            rawBody,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.True(result.Processed);
        Assert.Equal("AlreadyProcessing", result.Status);
        Assert.Equal(payment.Id, result.PaymentId);
        gateway.Verify(
            x => x.GetPaymentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        payments.Verify(
            x => x.UpdateAsync(It.IsAny<PaymentTransaction>()),
            Times.Never);
    }
}
