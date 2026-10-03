using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Events;

namespace HealthApp.Application.Events;

public sealed class LateSkipFeeRevenueHandler(IPlatformTransactionRepository transactions) : IDomainEventHandler<MealSkippedEvent>
{
    public async Task HandleAsync(MealSkippedEvent e, CancellationToken cancellationToken = default)
    {
        if (!e.IsLate || e.LateSkipFee <= 0) return;
        var reference = $"late-skip:{e.EventId:N}";
        if (await transactions.ExistsByReferenceAsync(reference)) return;

        await transactions.AddAsync(new PlatformTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = e.CustomerId,
            OutletId = e.OutletId,
            SubscriptionId = e.SubscriptionId,
            Type = "LateSkipFee",
            ReferenceId = reference,
            GrossAmount = e.LateSkipFee,
            PlatformFee = e.LateSkipFee,
            OutletAmount = 0m,
            FeePercent = 0m,
            Currency = "INR",
            Status = "Paid",
            CreatedAt = e.OccurredAtUtc
        });
    }
}
