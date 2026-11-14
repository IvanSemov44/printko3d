using System.Security.Cryptography;
using System.Text;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Application.Orders;

internal sealed class OrderLookupService(IRepositoryManager repositories, OrderLookupThrottle throttle) : IOrderLookupService
{
    private const int MaxEmailLength = 254;

    private const int MaxTokenLength = 64;

    public async Task<OrderDto> FindAsync(int number, string email, CancellationToken cancellationToken)
    {
        var wanted = email.Trim();
        var errors = new Dictionary<string, string[]>();
        if (number <= 0)
        {
            errors["number"] = ["The order number is required."];
        }

        if (wanted.Length is 0 or > MaxEmailLength)
        {
            errors["email"] = [$"The email is required and at most {MaxEmailLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("The request has invalid fields.", errors);
        }

        if (throttle.IsBlocked(number))
        {
            throw NotFound();
        }

        var order = await repositories.Order.GetByNumberAsync(number, cancellationToken);
        // Both answers are compared the same way, so a missing order costs the same time as a wrong email.
        var same = SameEmail(wanted, order?.Email ?? string.Empty);
        if (order is null || !same)
        {
            throttle.RecordFailure(number);
            throw NotFound();
        }

        return OrderMapper.ToDto(order);
    }

    public async Task<OrderDto> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var wanted = token.Trim();
        if (wanted.Length is 0 or > MaxTokenLength)
        {
            throw new ValidationFailedException(
                "The request has invalid fields.",
                new Dictionary<string, string[]> { ["token"] = ["The token is required."] });
        }

        // 256 random bits cannot be guessed, so a wrong token needs no counter; the limit of the address is enough.
        var order = await repositories.Order.GetByAccessTokenAsync(wanted, cancellationToken)
            ?? throw NotFound();
        return OrderMapper.ToDto(order);
    }

    private static bool SameEmail(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(right.ToUpperInvariant()));

    private static NotFoundException NotFound() =>
        new("No order was found for these details.", OrderErrorCodes.NotFound);
}
