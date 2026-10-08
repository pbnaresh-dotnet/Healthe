using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class ApplicationErrorRepository(HealthAppDbContext db) : IApplicationErrorRepository
{
    public async Task AddAsync(ApplicationErrorLog error, CancellationToken cancellationToken = default)
    {
        db.ApplicationErrorLogs.Add(error);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApplicationErrorLog> Items, int TotalCount)> QueryAsync(
        ApplicationErrorQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.ApplicationErrorLogs.AsNoTracking();

        if (request.OutletId.HasValue)
            query = query.Where(x => x.OutletId == request.OutletId);

        if (!string.IsNullOrWhiteSpace(request.Severity))
            query = query.Where(x => x.Severity == request.Severity.Trim());

        if (request.StatusCode.HasValue)
            query = query.Where(x => x.StatusCode == request.StatusCode.Value);

        if (request.Resolved.HasValue)
            query = query.Where(x => x.IsResolved == request.Resolved.Value);

        if (request.FromUtc.HasValue)
            query = query.Where(x => x.OccurredAtUtc >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(x => x.OccurredAtUtc <= request.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.Message.Contains(search) ||
                x.ErrorCode.Contains(search) ||
                x.Activity.Contains(search) ||
                x.RequestPath.Contains(search) ||
                x.ExceptionType.Contains(search) ||
                x.CorrelationId.Contains(search) ||
                x.TenantSlug.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ApplicationErrorLog?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ApplicationErrorLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ApplicationErrorLog?> ResolveAsync(
        Guid id,
        Guid resolvedByUserId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var error = await db.ApplicationErrorLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (error is null)
            return null;

        error.IsResolved = true;
        error.ResolvedAtUtc = DateTime.UtcNow;
        error.ResolvedByUserId = resolvedByUserId;
        error.ResolutionNotes = (notes ?? "").Trim();
        await db.SaveChangesAsync(cancellationToken);
        return error;
    }
}
