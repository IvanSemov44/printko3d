namespace Storefront.Application.Orders;

/// <summary>How a buyer without an account sees their own order (decision 0036).</summary>
public interface IOrderLookupService
{
    /// <summary>
    /// The order with this number, when the email is the one of the order (capitals do not matter). A wrong number and a wrong
    /// email give the same answer, so nobody can find out which numbers exist. After a few wrong emails for one number it
    /// answers the same for a while, even to the right one. Errors: 400 validation (a field is empty or too long),
    /// 404 order.not_found.
    /// </summary>
    Task<OrderDto> FindAsync(int number, string email, CancellationToken cancellationToken);

    /// <summary>The order that the key of the link in the email belongs to. Errors: 400 validation, 404 order.not_found.</summary>
    Task<OrderDto> FindByTokenAsync(string token, CancellationToken cancellationToken);
}
