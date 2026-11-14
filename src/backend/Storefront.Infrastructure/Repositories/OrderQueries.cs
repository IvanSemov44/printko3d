using Microsoft.EntityFrameworkCore;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Orders;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

/// <summary>The admin panel's list of orders.</summary>
internal sealed class OrderQueries(StorefrontDbContext context) : IOrderQueries
{
    public async Task<PagedResult<OrderAdminListItemDto>> GetAdminOrdersAsync(
        OrderAdminListQuery query,
        CancellationToken cancellationToken)
    {
        var orders = context.Orders.AsNoTracking();

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToList();
            orders = orders.Where(o => statuses.Contains(o.Status));
        }

        if (query.Search is { } search)
        {
            // % and _ typed by the admin are plain characters, not wildcards.
            var escape = ((char)92).ToString();
            var pattern = "%" + search.Replace(escape, escape + escape).Replace("%", escape + "%").Replace("_", escape + "_") + "%";
            var number = int.TryParse(search, out var parsed) ? parsed : -1;
            orders = orders.Where(o =>
                o.Number == number
                || EF.Functions.ILike(o.Email, pattern, escape)
                || EF.Functions.ILike(o.CustomerName, pattern, escape));
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var rows = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new
            {
                o.Number,
                o.Status,
                o.Currency,
                o.TotalAmount,
                o.CustomerName,
                o.Email,
                ItemCount = o.Items.Count,
                o.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(o => new OrderAdminListItemDto(
                o.Number, o.Status.ToString(), o.Currency, o.TotalAmount, o.CustomerName, o.Email, o.ItemCount, o.CreatedAt))
            .ToList();

        return new PagedResult<OrderAdminListItemDto>(items, query.Page, query.PageSize, totalCount);
    }
}
