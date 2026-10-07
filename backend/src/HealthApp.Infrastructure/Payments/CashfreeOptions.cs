namespace HealthApp.Infrastructure.Payments;

public sealed class CashfreeOptions
{
    public bool Enabled { get; set; }
    public string Environment { get; set; } = "Sandbox";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ApiVersion { get; set; } = "2025-01-01";
    public string BaseUrl { get; set; } = "";
    public string CustomerReturnUrl { get; set; } = "https://app.broccoly.in/payment";
    public string OutletReturnUrl { get; set; } = "https://broccoly.in/payment";
    public string WebhookUrl { get; set; } = "https://api.broccoly.in/api/payments/cashfree/webhook";
}
