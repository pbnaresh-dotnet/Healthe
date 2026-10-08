using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Payments;

public sealed class CashfreePaymentGateway(
    HttpClient http,
    IOptions<CashfreeOptions> configuredOptions) : IPaymentGateway
{
    private readonly CashfreeOptions options = configuredOptions.Value;

    public string Provider => "Cashfree";
    public string OutletReturnUrl => options.OutletReturnUrl;
    public string WebhookUrl => options.WebhookUrl;

    public async Task<PaymentGatewayCheckoutSession> CreateOrderAsync(
        PaymentGatewayCreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        using var message = CreateRequest(HttpMethod.Post, "/pg/orders");
        var payload = new
        {
            order_amount = request.Amount,
            order_currency = request.Currency,
            order_id = request.OrderId,
            customer_details = new
            {
                customer_id = request.CustomerId,
                customer_name = request.CustomerName,
                customer_email = request.CustomerEmail,
                customer_phone = request.CustomerPhone
            },
            order_meta = new
            {
                return_url = request.ReturnUrl,
                notify_url = request.NotifyUrl
            },
            order_note = request.OrderNote
        };
        message.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cashfree order creation failed ({(int)response.StatusCode}). {ExtractMessage(body)}");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var paymentSessionId = root.TryGetProperty("payment_session_id", out var session)
            ? session.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(paymentSessionId))
            throw new InvalidOperationException("Cashfree did not return a payment session ID.");

        var providerOrderId = root.TryGetProperty("order_id", out var order)
            ? order.GetString()
            : request.OrderId;

        var status = root.TryGetProperty("order_status", out var orderStatus)
            ? orderStatus.GetString()
            : "ACTIVE";

        return new(
            providerOrderId ?? request.OrderId,
            paymentSessionId,
            status ?? "ACTIVE");
    }

    public async Task<IReadOnlyList<PaymentGatewayTransactionStatus>> GetPaymentsAsync(
        string providerOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        using var message = CreateRequest(HttpMethod.Get, $"/pg/orders/{Uri.EscapeDataString(providerOrderId)}/payments");
        using var response = await http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cashfree payment status lookup failed ({(int)response.StatusCode}). {ExtractMessage(body)}");

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<PaymentGatewayTransactionStatus>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var paymentId = GetString(item, "cf_payment_id")
                ?? GetString(item, "payment_id")
                ?? "";
            var status = GetString(item, "payment_status") ?? "UNKNOWN";
            var messageText = GetString(item, "payment_message");
            var method = ExtractPaymentMethod(item);
            var amount = GetDecimal(item, "payment_amount") ?? GetDecimal(item, "order_amount");
            var currency = GetString(item, "payment_currency") ?? "INR";

            result.Add(new(
                paymentId,
                status,
                messageText,
                method,
                amount,
                currency));
        }

        return result;
    }

    public bool VerifyWebhookSignature(IReadOnlyDictionary<string, string> headers, string rawBody)
    {
        var signature = headers.TryGetValue("x-webhook-signature", out var sig) ? sig : "";
        var timestamp = headers.TryGetValue("x-webhook-timestamp", out var ts) ? ts : ""
    {
        if (!options.Enabled ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(options.ClientSecret))
            return false;

        var data = Encoding.UTF8.GetBytes(timestamp + rawBody);
        var secret = Encoding.UTF8.GetBytes(options.ClientSecret);
        using var hmac = new HMACSHA256(secret);
        var expected = Convert.ToBase64String(hmac.ComputeHash(data));

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(expected),
                Convert.FromBase64String(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("x-api-version", options.ApiVersion);
        message.Headers.TryAddWithoutValidation("x-client-id", options.ClientId);
        message.Headers.TryAddWithoutValidation("x-client-secret", options.ClientSecret);
        return message;
    }

    private void EnsureEnabled()
    {
        if (!options.Enabled)
            throw new InvalidOperationException("Cashfree payments are disabled. Enable Cashfree in backend configuration.");

        if (string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret))
            throw new InvalidOperationException("Cashfree credentials are not configured.");
    }

    private static string? GetString(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? GetDecimal(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static string? ExtractPaymentMethod(JsonElement item)
    {
        if (!item.TryGetProperty("payment_method", out var method))
            return null;

        if (method.ValueKind == JsonValueKind.String)
            return method.GetString();

        if (method.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "type", "upi", "card", "netbanking", "bank_transfer" })
            {
                if (method.TryGetProperty(propertyName, out var child))
                {
                    if (child.ValueKind == JsonValueKind.String)
                        return propertyName + ":" + child.GetString();
                    if (child.ValueKind == JsonValueKind.Object)
                        return propertyName;
                }
            }
        }

        return method.ToString();
    }

    private static string ExtractMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var name in new[] { "message", "type", "error", "code" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? "";
            }
        }
        catch
        {
            // Preserve the raw body when the gateway response is not JSON.
        }

        return string.IsNullOrWhiteSpace(body) ? "No additional details returned." : body[..Math.Min(body.Length, 500)];
    }
    public PaymentGatewayWebhookEvent? ParseWebhook(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var providerOrderId = ExtractString(root, "data", "order", "order_id")
            ?? ExtractString(root, "data", "payment", "cf_order_id")
            ?? ExtractString(root, "data", "payment", "order_id");
        if (string.IsNullOrWhiteSpace(providerOrderId))
            return null;
        var eventType = ExtractString(root, "type") ?? "";
        return new PaymentGatewayWebhookEvent(providerOrderId, eventType);
    }


}
