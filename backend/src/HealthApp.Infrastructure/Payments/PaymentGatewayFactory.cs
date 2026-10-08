using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Payments;

public sealed class PaymentGatewayFactory(
    IEnumerable<IPaymentGatewayAdapter> adapters,
    IOptions<PaymentGatewayOptions> options) : IPaymentGatewayFactory
{
    public IPaymentGateway Get(string provider)
    {
        var name = string.IsNullOrWhiteSpace(provider) ? options.Value.Provider : provider;
        var adapter = adapters.FirstOrDefault(x =>
            string.Equals(x.Provider, name.Trim(), StringComparison.OrdinalIgnoreCase));

        return adapter ?? throw new InvalidOperationException(
            $"Unsupported payment gateway '{name}'. Register an IPaymentGatewayAdapter for this provider.");
    }
}
