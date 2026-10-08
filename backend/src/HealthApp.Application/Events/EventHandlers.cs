using HealthApp.Application.Abstractions;
using HealthApp.Domain.Events;

namespace HealthApp.Application.Events;

public sealed class LateSkipFeeRevenueHandler(IPlatformTransactionRepository transactions, ICustomerCreditRepository credits) : IDomainEventHandler<MealSkippedEvent>
{
    public async Task HandleAsync(MealSkippedEvent e, CancellationToken cancellationToken = default)
    {
        if (!e.IsLate || e.LateSkipFee <= 0) return;
        var reference = $"late-skip:{e.EventId:N}";
        if (await transactions.ExistsByReferenceAsync(reference)) return;

        await transactions.AddAsync(new HealthApp.Domain.Entities.PlatformTransaction
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
            Status = await credits.GetBalanceAsync(e.CustomerId) >= 0m ? "Paid" : "Pending",
            CreatedAt = e.OccurredAtUtc
        });
    }
}

public sealed class CustomerMealSkippedEmailHandler(
    IUserRepository users,
    ISubscriptionMealSelectionRepository selections,
    IRecipeRepository recipes,
    ITransactionalEmailService emails) : IDomainEventHandler<MealSkippedEvent>
{
    public async Task HandleAsync(MealSkippedEvent e, CancellationToken cancellationToken = default)
    {
        var customer = await users.FindByIdAsync(e.CustomerId);
        if (customer is null) return;

        var selection = await selections.GetAsync(e.MealSelectionId);
        var recipe = selection is null ? null : await recipes.GetForOutletAsync(selection.RecipeId, e.OutletId);

        await emails.TrySendAsync(
            EmailTemplateId.MealSkipped,
            customer.Email,
            new Dictionary<string, string?>
            {
                ["FirstName"] = customer.FirstName,
                ["MealDate"] = selection?.MealDate.ToString("dd MMM yyyy") ?? e.OccurredAtUtc.ToString("dd MMM yyyy"),
                ["MealName"] = recipe?.Name ?? "Scheduled meal",
                ["Reason"] = string.IsNullOrWhiteSpace(e.Reason) ? "Customer requested skip." : e.Reason,
                ["LateSkipFee"] = $"₹{e.LateSkipFee:N2}"
            },
            cancellationToken);
    }
}

public sealed class CustomerMealRescheduledEmailHandler(
    IUserRepository users,
    ISubscriptionMealSelectionRepository selections,
    IRecipeRepository recipes,
    ITransactionalEmailService emails) : IDomainEventHandler<MealRescheduledEvent>
{
    public async Task HandleAsync(MealRescheduledEvent e, CancellationToken cancellationToken = default)
    {
        var customer = await users.FindByIdAsync(e.CustomerId);
        if (customer is null) return;

        var selection = await selections.GetAsync(e.MealSelectionId);
        var recipe = selection is null ? null : await recipes.GetForOutletAsync(selection.RecipeId, e.OutletId);

        await emails.TrySendAsync(
            EmailTemplateId.MealRescheduled,
            customer.Email,
            new Dictionary<string, string?>
            {
                ["FirstName"] = customer.FirstName,
                ["MealName"] = recipe?.Name ?? "Scheduled meal",
                ["OldDate"] = e.OldDate.ToString("dd MMM yyyy"),
                ["NewDate"] = e.NewDate.ToString("dd MMM yyyy")
            },
            cancellationToken);
    }
}
