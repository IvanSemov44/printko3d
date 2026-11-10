using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

public sealed record ProductAdminListQuery(ProductStatus? Status, Guid? CategoryId, string? Search, int Page, int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
    public const int MaxSearchLength = 100;

    /// <summary>Validates the raw request values. Throws <see cref="ValidationFailedException"/> listing every bad parameter.</summary>
    public static ProductAdminListQuery Create(string? status, Guid? categoryId, string? search, int page, int pageSize)
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

        ProductStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status))
        {
            // Matched by name on purpose: Enum.TryParse would also accept numbers such as "1".
            var name = Enum.GetNames<ProductStatus>()
                .FirstOrDefault(n => string.Equals(n, status, StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                parsedStatus = Enum.Parse<ProductStatus>(name);
            }
            else
            {
                errors["status"] = ["status must be Draft, Published or Archived."];
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

        return new ProductAdminListQuery(parsedStatus, categoryId, trimmedSearch, page, pageSize);
    }
}
