using HealthApp.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure.Payments;

public sealed class PaymentRetryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PaymentRetryOptions> options,
    ILogger<PaymentRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Payment retry worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 5, 300));
        var maxAttempts = Math.Clamp(settings.MaxAttempts, 1, 20);
        var batchSize = Math.Clamp(settings.BatchSize, 1, 100);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(maxAttempts, batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Payment retry worker batch failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(int maxAttempts, int batchSize, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        var now = DateTime.UtcNow;
        var candidates = await payments.GetRetryableAsync(now, maxAttempts, batchSize);

        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var claimed = await payments.TryClaimRetryAsync(candidate.Id, now, maxAttempts);
            if (!claimed)
                continue;

            try
            {
                await paymentService.RetryPendingAsync(candidate.Id, cancellationToken);
                logger.LogInformation(
                    "Payment retry completed for {PaymentId} provider order {ProviderOrderId}.",
                    candidate.Id,
                    candidate.ProviderOrderId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Payment retry failed for {PaymentId} provider order {ProviderOrderId}; retry state was persisted.",
                    candidate.Id,
                    candidate.ProviderOrderId);
            }
        }
    }
}
