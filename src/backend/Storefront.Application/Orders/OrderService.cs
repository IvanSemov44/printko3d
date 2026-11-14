using System.Net.Mail;
using System.Text.RegularExpressions;
using Storefront.Application.Carts;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Application.Payments;
using Storefront.Application.Shipping;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed partial class OrderService(
    IRepositoryManager repositories,
    IOrderNumberGenerator numbers,
    TimeProvider timeProvider,
    OrderOptions options,
    IShippingProvider shipping,
    PaymentOptions paymentOptions) : IOrderService
{
    private const int NumberAttempts = 5;

    public async Task<OrderDto> PlaceAsync(string? cartToken, PlaceOrderInput input, CancellationToken cancellationToken)
    {
        var fields = Validate(input);

        var hash = Array.Empty<byte>();
        var cart = CartToken.TryHash(cartToken, out hash)
            ? await repositories.Cart.GetByTokenHashAsync(hash, trackChanges: true, cancellationToken)
            : null;
        if (cart is null || cart.Items.Count == 0)
        {
            throw new RuleViolationException("The cart is empty.", "order.cart_empty");
        }

        var lines = (await repositories.CartQueries.GetAsync(hash, cancellationToken)).Lines;
        if (lines.Count != cart.Items.Count)
        {
            throw CartChanged();
        }

        if (lines.Any(line => line.State != CartLineState.Available))
        {
            throw new RuleViolationException(
                "A line of the cart cannot be ordered. Open the cart and remove it.", "order.cart_not_orderable");
        }

        var delivery = await PriceDeliveryAsync(lines, fields, cancellationToken);
        var total = lines.Sum(line => line.LinePrice) + delivery.Amount;
        if (total != input.ExpectedTotal)
        {
            throw new ConflictException(
                "The cart is worth something else than it was in the preview. Check it and confirm again.",
                "order.total_changed");
        }

        var settings = await repositories.StoreSettings.GetAsync(trackChanges: false, cancellationToken)
            ?? throw new InvalidOperationException("The store settings are missing.");

        var order = new Order
        {
            Number = await FreeNumberAsync(cancellationToken),
            Currency = settings.DefaultCurrency,
            TotalAmount = total,
            ShippingMethod = delivery.Code,
            ShippingAmount = delivery.Amount,
            CustomerName = fields.Name,
            Email = fields.Email,
            Phone = fields.Phone,
            ShippingStreet = fields.Street,
            ShippingCity = fields.City,
            ShippingPostalCode = fields.PostalCode,
            ShippingCountry = ShippingMethods.Country,
            TermsVersion = options.TermsVersion,
            TermsAcceptedAt = timeProvider.GetUtcNow(),
            // Payment turned off: nobody can pay, so the order has no deadline and is never cancelled for it.
            PaymentDueAt = paymentOptions.Enabled ? timeProvider.GetUtcNow().AddMinutes(paymentOptions.WindowMinutes) : null,
            Items = lines.Select(line => new OrderItem
            {
                VariantId = line.VariantId,
                ProductName = line.ProductName,
                VariantName = line.VariantName,
                Sku = line.Sku,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                LineTotal = line.LinePrice,
                FulfilmentType = line.FulfilmentType,
                LeadTimeDays = line.LeadTimeDays,
            }).ToList(),
        };

        // The order, the removal of the lines and the stock are one transaction: if any part fails, nothing stays.
        // A second order made from the same cart at the same moment deletes lines that are already gone, which the
        // database reports; it is translated to cart.changed, and the stock it took goes back with the rollback.
        await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);
        await TakeStockAsync(order, cancellationToken);
        repositories.Order.Create(order);
        repositories.Cart.RemoveItems(cart);
        repositories.Cart.Touch(cart);
        await repositories.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OrderMapper.ToDto(order);
    }

    /// <summary>
    /// Asks the courier for the price of the method the buyer chose. The price is never taken from the buyer: a method
    /// the courier does not offer for this parcel is refused, and a parcel it cannot carry is refused by the courier.
    /// </summary>
    private async Task<ShippingQuote> PriceDeliveryAsync(
        IReadOnlyList<CartLineDto> lines,
        PlaceOrderInput fields,
        CancellationToken cancellationToken)
    {
        var quotes = await shipping.GetQuotesAsync(
            new ShippingRequest(ParcelWeight.Of(lines), ShippingMethods.Country, fields.City, fields.PostalCode),
            cancellationToken);
        return quotes.FirstOrDefault(quote => quote.Code == fields.ShippingMethod)
            ?? throw new RuleViolationException(
                "The chosen delivery is not available. Open the checkout and choose again.",
                "order.shipping_unavailable");
    }

    /// <summary>
    /// Takes the stock of every line that is in stock, in the order of the variant ids, so that two orders with the same
    /// lines never wait for each other in a circle. The statement is conditional: if someone took the piece between the
    /// cart check and now, it refuses and the transaction goes back.
    /// </summary>
    private async Task TakeStockAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var item in StockLines(order))
        {
            if (!await repositories.Product.TryTakeStockAsync(item.VariantId, item.Quantity, cancellationToken))
            {
                throw new ConflictException(
                    "Someone else took the last pieces at the same moment. Open the cart and check it.",
                    OrderErrorCodes.StockChanged);
            }
        }
    }

    public async Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await repositories.Order.GetByIdAsync(orderId, trackChanges: false, cancellationToken)
            ?? throw new NotFoundException($"Order {orderId} was not found.", "order.not_found");

        await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);
        if (await OrderStockLines.GiveBackAsync(repositories, order, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static IEnumerable<OrderItem> StockLines(Order order) => OrderStockLines.Of(order);

    // A clash of two random numbers is rare; the unique index is the guard and this loop is the polite path.
    private async Task<int> FreeNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < NumberAttempts; attempt++)
        {
            var number = numbers.Next();
            if (!await repositories.Order.NumberExistsAsync(number, cancellationToken))
            {
                return number;
            }
        }

        throw new InvalidOperationException("No free order number was found.");
    }

    private static PlaceOrderInput Validate(PlaceOrderInput input)
    {
        var fields = input with
        {
            Name = input.Name.Trim(),
            Email = input.Email.Trim(),
            Phone = input.Phone.Trim(),
            Street = input.Street.Trim(),
            City = input.City.Trim(),
            PostalCode = input.PostalCode.Trim(),
            ShippingMethod = input.ShippingMethod.Trim(),
        };

        var errors = new Dictionary<string, string[]>();
        Required(errors, "name", fields.Name, 200);
        if (Required(errors, "email", fields.Email, 254) && !IsEmail(fields.Email))
        {
            errors["email"] = ["The email address is not valid."];
        }

        if (Required(errors, "phone", fields.Phone, 30) && !IsPhone(fields.Phone))
        {
            errors["phone"] = ["The phone number is not valid."];
        }

        Required(errors, "street", fields.Street, 200);
        Required(errors, "city", fields.City, 100);
        if (Required(errors, "postalCode", fields.PostalCode, 20) && !PostalCodePattern().IsMatch(fields.PostalCode))
        {
            errors["postalCode"] = ["The postal code is four digits."];
        }

        if (fields.ShippingMethod.Length == 0)
        {
            errors["shippingMethod"] = ["The delivery method is required."];
        }

        if (!fields.TermsAccepted)
        {
            errors["termsAccepted"] = ["The terms and conditions must be accepted."];
        }

        return errors.Count == 0
            ? fields
            : throw new ValidationFailedException("The order has invalid fields.", errors);
    }

    // Returns true when the value is there and not too long, so the caller can go on to the format.
    private static bool Required(Dictionary<string, string[]> errors, string field, string value, int maxLength)
    {
        if (value.Length == 0)
        {
            errors[field] = ["The field is required."];
            return false;
        }

        if (value.Length > maxLength)
        {
            errors[field] = [$"The field is at most {maxLength} characters."];
            return false;
        }

        return true;
    }

    private static bool IsEmail(string value) =>
        !value.Any(char.IsWhiteSpace)
        && MailAddress.TryCreate(value, out var address)
        && address.Address == value;

    private static bool IsPhone(string value) =>
        PhonePattern().IsMatch(value) && value.Count(char.IsAsciiDigit) >= 7;

    private static ConflictException CartChanged() =>
        new("The cart was changed at the same moment. Open it and try again.", "cart.changed");

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex(@"^[0-9+()\- ]{7,20}$")]
    private static partial Regex PhonePattern();
}
