using System.Net;
using HealthApp.Application.Abstractions;
using HealthApp.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace HealthApp.Application.Tests;

public sealed class CashfreePaymentGatewayTests
{
    [Fact]
    public async Task CreateOrder_ReusesSameIdempotencyKeyAcrossTransientRetry()
    {
        var seenKeys = new List<string>();
        var attempts = 0;

        var handler = new StubHandler(async request =>
        {
            attempts++;
            seenKeys.Add(request.Headers.GetValues("x-idempotency-key").Single());

            if (attempts == 1)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"message\":\"temporary\"}")
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"order_id\":\"ORDER-1\",\"payment_session_id\":\"SESSION-1\",\"order_status\":\"ACTIVE\"}")
            };
        });

        var options = Options.Create(new CashfreeOptions
        {
            Enabled = true,
            ClientId = "client",
            ClientSecret = "secret",
            ApiVersion = "2025-01-01",
            WebhookUrl = "https://api.example.com/api/payments/webhook",
            OutletReturnUrl = "https://outlet.example.com/payment"
        });

        var gateway = new CashfreePaymentGateway(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sandbox.cashfree.com")
        }, options);

        var result = await gateway.CreateOrderAsync(new PaymentGatewayCreateOrderRequest(
            "ORDER-1", 100m, "INR", "customer", "Test Customer",
            "customer@example.com", "9999999999",
            "https://outlet.example.com/payment",
            "https://api.example.com/api/payments/webhook",
            "Test order", "idem-123"));

        Assert.Equal("SESSION-1", result.PaymentSessionId);
        Assert.Equal(2, attempts);
        Assert.Equal(new[] { "idem-123", "idem-123" }, seenKeys);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}