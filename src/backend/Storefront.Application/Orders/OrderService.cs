using System.Net.Mail;
using System.Text.RegularExpressions;
using Storefront.Application.Carts;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed partial class OrderService(
    IRepositoryManager repositories,
    IOrderNumberGenerator numbers,
    TimeProvider timeProvider,
    OrderOptions options) : IOrderService
{
    private const int NumberAttempts = 5;

    private const string Country = "BG";

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

        var total = lines.Sum(line => line.LinePrice);
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
            CustomerName = fields.Name,
            Email = fields.Email,
            Phone = fields.Phone,
            ShippingStreet = fields.Street,
            ShippingCity = fields.City,
            ShippingPostalCode = fields.PostalCode,
            ShippingCountry = Country,
            TermsVersion = options.TermsVersion,
            TermsAcceptedAt = timeProvider.GetUtcNow(),
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

        // The order and the removal of the lines are one save. A second order made from the same cart at the same
        // moment deletes lines that are already gone, which the database reports; it is translated to cart.changed.
        repositories.Order.Create(order);
        repositories.Cart.RemoveItems(cart);
        repositories.Cart.Touch(cart);
        await repositories.SaveAsync(cancellationToken);

        return ToDto(order);
    }

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

    private static OrderDto ToDto(Order order) => new(
        order.Number,
        order.Status.ToString(),
        order.Currency,
        order.TotalAmount,
        order.CustomerName,
        order.Email,
        order.Phone,
        order.ShippingStreet,
        order.ShippingCity,
        order.ShippingPostalCode,
        order.ShippingCountry,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemDto(
            i.ProductName,
            i.VariantName,
            i.Sku,
            i.UnitPrice,
            i.Quantity,
            i.LineTotal,
            i.FulfilmentType,
            i.LeadTimeDays)).ToList());

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex(@"^[0-9+()\- ]{7,20}$")]
    private static partial Regex PhonePattern();
}
