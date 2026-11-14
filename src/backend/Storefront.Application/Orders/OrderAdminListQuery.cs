using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

public sealed record OrderAdminListQuery(IReadOnlyList<OrderStatus> Statuses, string? Search, int Page, int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
    public const int MaxSearchLength = 100;
    public const int MaxStatuses = 10;

    /// <summary>Validates the raw request values. Throws <see cref="ValidationFailedException"/> listing every bad parameter.</summary>
    public static OrderAdminListQuery Create(IReadOnlyList<string>? statuses, string? search, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1 || page > MaxPage)
        {
            errors["page"] = [$"page must be between 1 and {MaxPage}."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        var parsed = new List<OrderStatus>();
        foreach (var value in (statuses ?? []).Take(MaxStatuses + 1))
        {
            // Matched by name on purpose: Enum.TryParse would also accept numbers such as "1".
            var name = Enum.GetNames<OrderStatus>()
                .FirstOrDefault(n => string.Equals(n, value?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                errors["status"] = ["status must be the name of an order status."];
                break;
            }

            var status = Enum.Parse<OrderStatus>(name);
            if (!parsed.Contains(status))
            {
                parsed.Add(status);
            }
        }

        var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (trimmedSearch is { Length: > MaxSearchLength })
        {
            errors["search"] = [$"search must be at most {MaxSearchLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("One or more query parameters are invalid.", errors);
        }

        return new OrderAdminListQuery(parsed, trimmedSearch, page, pageSize);
    }
}
