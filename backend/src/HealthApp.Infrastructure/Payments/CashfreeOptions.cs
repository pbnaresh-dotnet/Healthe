namespace HealthApp.Infrastructure.Payments;

public sealed class PaymentGatewayOptions
{
    public string Provider { get; set; } = "Cashfree";
}

public sealed class CashfreeOptions
{
    public bool Enabled { get; set; }
    public string Environment { get; set; } = "Sandbox";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ApiVersion { get; set; } = "2025-01-01";
    public string BaseUrl { get; set; } = "";
    public string OutletReturnUrl { get; set; } = "https://broccoly.in/payment";
    public string WebhookUrl { get; set; } = "https://api.broccoly.in/api/payments/webhook";
}

public sealed class PaymentRetryOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int MaxAttempts { get; set; } = 6;
    public int BatchSize { get; set; } = 20;
}
