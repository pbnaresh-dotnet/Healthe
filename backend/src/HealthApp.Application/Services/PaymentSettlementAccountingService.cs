using System.Globalization;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;

namespace HealthApp.Application.Services;

public sealed class PaymentSettlementAccountingService(
    IPaymentTransactionRepository payments,
    IPaymentGatewaySettlementRepository settlements,
    IPaymentSettlementReconciliationExceptionRepository exceptions) : IPaymentSettlementAccountingService
{
    public async Task<PaymentGatewaySettlement> RecordSettlementAsync(
        PaymentGatewaySettlementInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.PaymentTransactionId == Guid.Empty)
            throw new ArgumentException("Payment transaction is required.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.ProviderSettlementId))
            throw new ArgumentException("Provider settlement ID is required.", nameof(input));
        if (input.GatewayFeeAmount < 0m || input.GatewayFeeTaxAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(input), "Gateway fee amounts cannot be negative.");

        var payment = await payments.GetAsync(input.PaymentTransactionId)
            ?? throw new KeyNotFoundException("Payment transaction not found.");

        if (!string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only paid payment transactions can be reconciled.");

        var existing = await settlements.GetByPaymentTransactionAsync(payment.Id);
        if (existing is not null)
            return existing;

        if (input.ReportedGrossAmount.HasValue &&
            Math.Round(input.ReportedGrossAmount.Value, 2) != Math.Round(payment.Amount, 2))
            throw new InvalidOperationException("Provider settlement gross amount does not match the paid payment amount.");

        if (input.GatewayFeeAmount + input.GatewayFeeTaxAmount > payment.Amount + Math.Max(0m, input.OtherProviderAdjustmentAmount))
            throw new InvalidOperationException("Gateway deductions exceed the payment amount.");

        var net = Math.Round(
            payment.Amount
            - input.GatewayFeeAmount
            - input.GatewayFeeTaxAmount
            + input.OtherProviderAdjustmentAmount,
            2,
            MidpointRounding.AwayFromZero);

        if (input.ReportedNetSettlementAmount.HasValue &&
            Math.Round(input.ReportedNetSettlementAmount.Value, 2) != net)
            throw new InvalidOperationException("Provider reported net settlement does not match the calculated net settlement.");

        var settlement = new PaymentGatewaySettlement
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            OutletId = payment.OutletId,
            Provider = payment.Provider,
            ProviderPaymentId = payment.ProviderPaymentId,
            ProviderSettlementId = input.ProviderSettlementId.Trim(),
            GrossAmount = payment.Amount,
            GatewayFeeAmount = Math.Round(input.GatewayFeeAmount, 2, MidpointRounding.AwayFromZero),
            GatewayFeeTaxAmount = Math.Round(input.GatewayFeeTaxAmount, 2, MidpointRounding.AwayFromZero),
            OtherProviderAdjustmentAmount = Math.Round(input.OtherProviderAdjustmentAmount, 2, MidpointRounding.AwayFromZero),
            NetSettlementAmount = net,
            Currency = payment.Currency,
            Status = "Reconciled",
            ReconciliationReference = input.ReconciliationReference?.Trim() ?? "",
            SourceDataJson = input.SourceDataJson ?? "",
            SettledAtUtc = input.SettledAtUtc,
            ReconciledAtUtc = DateTime.UtcNow,
            ReconciledBy = input.ReconciledBy?.Trim() ?? ""
        };

        await settlements.AddAsync(settlement);
        return settlement;
    }

    public async Task<PaymentSettlementImportResultDto> ImportCsvAsync(
        string provider,
        Stream csv,
        string reconciledBy,
        CancellationToken cancellationToken = default)
    {
        provider = string.IsNullOrWhiteSpace(provider) ? "Cashfree" : provider.Trim();
        if (!string.Equals(provider, "Cashfree", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("This settlement importer currently supports Cashfree reports only.", nameof(provider));

        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new ArgumentException("Settlement CSV is empty.", nameof(csv));

        var headers = ParseCsvLine(headerLine).Select(NormalizeHeader).ToArray();
        var required = new[] { "paymentid", "settlementid", "gatewayfeeamount", "gatewayfeetaxamount" };
        var missing = required.Where(x => !headers.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException($"Cashfree settlement CSV is missing required columns: {string.Join(", ", missing)}.", nameof(csv));

        var total = 0;
        var reconciled = 0;
        var already = 0;
        var unmatched = 0;
        var errors = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            total++;

            var cells = ParseCsvLine(line);
            var row = headers
                .Select((h, i) => new { h, value = i < cells.Count ? cells[i] : "" })
                .GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().value, StringComparer.OrdinalIgnoreCase);

            var paymentId = Get(row, "paymentid", "cfpaymentid", "providerpaymentid").Trim();
            var settlementId = Get(row, "settlementid", "providersettlementid").Trim();
            var rawJson = JsonSerializer.Serialize(row);

            try
            {
                if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(settlementId))
                    throw new ImportRowException("MissingProviderIdentifier", "Provider payment ID and settlement ID are required.");

                var payment = await payments.GetByProviderPaymentIdAsync(provider, paymentId);
                if (payment is null)
                {
                    unmatched++;
                    await PersistExceptionAsync(provider, paymentId, settlementId, "PaymentNotFound", rawJson,
                        null, null, "No paid payment transaction matches the provider payment ID.", cancellationToken);
                    errors.Add($"Row {total}: payment '{paymentId}' was not found.");
                    continue;
                }

                var existing = await settlements.GetByPaymentTransactionAsync(payment.Id);
                if (existing is not null)
                {
                    already++;
                    continue;
                }

                if (!string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    throw new ImportRowException("PaymentNotPaid", "Matched payment transaction is not Paid.");

                var fee = ParseMoney(Get(row, "gatewayfeeamount", "servicecharge", "fee", "processingfee"));
                var feeTax = ParseMoney(Get(row, "gatewayfeetaxamount", "servicetax", "feetax", "gstonfee"));
                var gross = ParseNullableMoney(Get(row, "reportedgrossamount", "transactionamount", "grossamount", "amount"));
                var reportedNet = ParseNullableMoney(Get(row, "reportednetsettlementamount", "settlementamount", "netsettlementamount", "netamount"));
                var adjustment = ParseMoney(Get(row, "otherprovideradjustmentamount", "adjustmentamount", "adjustment", "refundadjustment", "disputeadjustment"));
                if (!adjustment.HasValue && reportedNet.HasValue)
                    adjustment = reportedNet.Value - payment.Amount + fee + feeTax;

                var settledAt = ParseNullableDate(Get(row, "settledatutc", "settlementdate", "settleddate"));
                var reference = Get(row, "reconciliationreference", "utr", "bankreference", "settlementreference").Trim();

                var input = new PaymentGatewaySettlementInput(
                    payment.Id,
                    settlementId,
                    fee ?? 0m,
                    feeTax ?? 0m,
                    adjustment ?? 0m,
                    settledAt,
                    rawJson,
                    reference,
                    reconciledBy,
                    gross,
                    reportedNet);

                await RecordSettlementAsync(input, cancellationToken);
                reconciled++;
            }
            catch (ImportRowException ex)
            {
                unmatched++;
                await PersistExceptionAsync(provider, paymentId, settlementId, ex.Type, rawJson, null, null, ex.Message, cancellationToken);
                errors.Add($"Row {total}: {ex.Message}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or ArgumentException)
            {
                unmatched++;
                await PersistExceptionAsync(provider, paymentId, settlementId, "ValidationFailure", rawJson, null, null, ex.Message, cancellationToken);
                errors.Add($"Row {total}: {ex.Message}");
            }
        }

        return new PaymentSettlementImportResultDto(provider, total, reconciled, already, unmatched, errors);
    }

    private async Task PersistExceptionAsync(
        string provider,
        string paymentId,
        string settlementId,
        string type,
        string rawJson,
        decimal? gross,
        decimal? net,
        string message,
        CancellationToken cancellationToken)
    {
        var existing = await exceptions.GetOpenAsync(provider, paymentId, settlementId, type);
        if (existing is not null) return;

        await exceptions.AddAsync(new PaymentSettlementReconciliationException
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderPaymentId = paymentId,
            ProviderSettlementId = settlementId,
            ExceptionType = type,
            Status = "Open",
            ReportedGrossAmount = gross,
            ReportedNetSettlementAmount = net,
            RawRowJson = rawJson,
            ErrorMessage = message,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static string Get(IReadOnlyDictionary<string, string> row, params string[] keys) =>
        keys.Select(k => NormalizeHeader(k))
            .Select(k => row.TryGetValue(k, out var value) ? value : "")
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static decimal? ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Replace(",", "").Replace("₹", "", StringComparison.OrdinalIgnoreCase);
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var amount))
            return amount;
        throw new FormatException($"Invalid monetary value '{value}'.");
    }

    private static DateTime? ParseNullableDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        throw new FormatException($"Invalid settlement date '{value}'.");
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private sealed class ImportRowException(string type, string message) : Exception(message)
    {
        public string Type { get; } = type;
    }
}