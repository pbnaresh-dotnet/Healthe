namespace HealthApp.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record SubscriptionCreatedEvent(Guid SubscriptionId, Guid CustomerId, Guid OutletId) : DomainEventBase;
public sealed record MealSkippedEvent(Guid SubscriptionId, Guid MealSelectionId, Guid CustomerId, Guid OutletId, decimal MealAmount, decimal DeliveryAmount, bool IsLate, decimal LateSkipFee, string Reason) : DomainEventBase;
public sealed record MealRescheduledEvent(Guid SubscriptionId, Guid MealSelectionId, DateTime OldDate, DateTime NewDate, Guid CustomerId, Guid OutletId) : DomainEventBase;
