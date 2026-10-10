using System.Collections;
using System.Reflection;

namespace HealthApp.Api;

/// <summary>
/// Optional compatibility-preserving list filtering for API list endpoints.
/// Query parameters are opt-in so existing clients that expect arrays are not broken.
/// </summary>
internal sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

internal static class ListQuery
{
    public static IReadOnlyList<T> Apply<T>(
        IEnumerable<T> source,
        string? search,
        int? page,
        int? pageSize,
        string? status = null,
        DateTime? date = null)
    {
        var query = source ?? Enumerable.Empty<T>();
        var searchable = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && (p.PropertyType == typeof(string) ||
                Nullable.GetUnderlyingType(p.PropertyType) == typeof(string)))
            .ToArray();
        var statusProperty = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => p.CanRead && string.Equals(p.Name, "Status", StringComparison.OrdinalIgnoreCase));
        var dateProperties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?)))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item is not null && searchable.Any(property =>
                (property.GetValue(item)?.ToString() ?? string.Empty)
                    .Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(status) && statusProperty is not null)
        {
            query = query.Where(item => item is not null &&
                string.Equals(statusProperty.GetValue(item)?.ToString(), status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (date.HasValue && dateProperties.Length > 0)
        {
            var target = date.Value.Date;
            query = query.Where(item => item is not null && dateProperties.Any(property =>
            {
                var value = property.GetValue(item);
                return value is DateTime dt && dt.Date == target;
            }));
        }

        // Preserve the existing full-array contract for older clients that omit pagination.
        if (!page.HasValue && !pageSize.HasValue) return query.ToArray();
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 50, 1, 200);
        var totalCount = query.Count();
        var items = query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToArray();
        return new PagedList<T>(items, totalCount, currentPage, currentPageSize);
    }
}