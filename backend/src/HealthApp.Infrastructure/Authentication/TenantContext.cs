using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure.Authentication;

public sealed class TenantContext : ITenantContext
{
    public Guid? OutletId { get; private set; }
    public string? OutletSlug { get; private set; }
    public bool IsResolved => OutletId.HasValue;

    public void Set(Guid outletId, string outletSlug)
    {
        if (outletId == Guid.Empty)
            throw new ArgumentException("A tenant outlet id is required.", nameof(outletId));

        OutletId = outletId;
        OutletSlug = outletSlug.Trim().ToLowerInvariant();
    }
}
