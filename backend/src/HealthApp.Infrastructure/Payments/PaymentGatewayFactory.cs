using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure.Payments;

public sealed class PaymentGatewayFactory(
    CashfreePaymentGateway cashfree,
    PaymentGatewayOptions options) : IPaymentGatewayFactory
{
    public IPaymentGateway Get(string provider)
    {
        var name = string.IsNullOrWhiteSpace(provider) ? options.Provider : provider;
        return name.Trim().ToLowerInvariant() switch
        {
            "cashfree" => cashfree,
            _ => throw new InvalidOperationException(
                $"Unsupported payment gateway '{name}'. Configure a registered payment-gateway adapter.")
        };
    }
}
