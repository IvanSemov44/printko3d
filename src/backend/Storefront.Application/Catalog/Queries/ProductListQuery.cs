using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

public sealed record ProductListQuery(
    string? CategorySlug,
    FulfilmentType? FulfilmentType,
    ProductSort Sort,
    int Page,
    int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;
    public const int MaxPage = 10_000;

    /// <summary>Validates the raw request values. Throws <see cref="ValidationFailedException"/> listing every bad parameter.</summary>
    public static ProductListQuery Create(string? category, string? fulfilmentType, string? sort, int page, int pageSize)
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

        FulfilmentType? parsedFulfilment = null;
        if (!string.IsNullOrEmpty(fulfilmentType))
        {
            // Matched by name on purpose: Enum.TryParse would also accept numbers such as "1".
            var name = Enum.GetNames<FulfilmentType>()
                .FirstOrDefault(n => string.Equals(n, fulfilmentType, StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                parsedFulfilment = Enum.Parse<FulfilmentType>(name);
            }
            else
            {
                errors["fulfilmentType"] = ["fulfilmentType must be InStock or MadeToOrder."];
            }
        }

        var parsedSort = ProductSort.NameAscending;
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort)
            {
                case "name":
                    parsedSort = ProductSort.NameAscending;
                    break;
                case "-name":
                    parsedSort = ProductSort.NameDescending;
                    break;
                case "price":
                    parsedSort = ProductSort.PriceAscending;
                    break;
                case "-price":
                    parsedSort = ProductSort.PriceDescending;
                    break;
                default:
                    errors["sort"] = ["sort must be one of: name, -name, price, -price."];
                    break;
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("One or more query parameters are invalid.", errors);
        }

        return new ProductListQuery(
            string.IsNullOrWhiteSpace(category) ? null : category,
            parsedFulfilment,
            parsedSort,
            page,
            pageSize);
    }
}
