using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;

namespace HealthApp.Application.Services;

public sealed class PaymentSettlementAccountingService(
    IPaymentTransactionRepository payments,
    IPaymentGatewaySettlementRepository settlements) : IPaymentSettlementAccountingService
{
    public async Task<PaymentGatewaySettlement> RecordSettlementAsync(
        PaymentGatewaySettlementInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.PaymentTransactionId == Guid.Empty)
            throw new ArgumentException("Payment transaction is required.", nameof(input));
        if (input.GatewayFeeAmount < 0m || input.GatewayFeeTaxAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(input), "Gateway fee amounts cannot be negative.");

        var payment = await payments.GetAsync(input.PaymentTransactionId)
            ?? throw new KeyNotFoundException("Payment transaction not found.");

        if (!string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only paid payment transactions can be reconciled.");

        var existing = await settlements.GetByPaymentTransactionAsync(payment.Id);
        if (existing is not null)
            return existing;

        if (input.GatewayFeeAmount + input.GatewayFeeTaxAmount > payment.Amount + Math.Max(0m, input.OtherProviderAdjustmentAmount))
            throw new InvalidOperationException("Gateway deductions exceed the payment amount.");

        var net = Math.Round(
            payment.Amount
            - input.GatewayFeeAmount
            - input.GatewayFeeTaxAmount
            + input.OtherProviderAdjustmentAmount,
            2,
            MidpointRounding.AwayFromZero);

        var settlement = new PaymentGatewaySettlement
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            OutletId = payment.OutletId,
            Provider = payment.Provider,
            ProviderPaymentId = payment.ProviderPaymentId,
            ProviderSettlementId = input.ProviderSettlementId?.Trim() ?? "",
            GrossAmount = payment.Amount,
            GatewayFeeAmount = Math.Round(input.GatewayFeeAmount, 2),
            GatewayFeeTaxAmount = Math.Round(input.GatewayFeeTaxAmount, 2),
            OtherProviderAdjustmentAmount = Math.Round(input.OtherProviderAdjustmentAmount, 2),
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
}
