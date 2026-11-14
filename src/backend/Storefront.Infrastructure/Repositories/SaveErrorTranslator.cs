using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Application.Exceptions;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;

namespace Storefront.Infrastructure.Repositories;

/// <summary>
/// Turns database failures into <see cref="ConflictException"/> with the code of the record that failed.
/// A failure that is not listed here is not hidden: the caller gets the original exception.
/// </summary>
internal static class SaveErrorTranslator
{
    private const string UniqueViolation = "23505";

    private static readonly Dictionary<string, (string Message, string Code)> UniqueIndexes = new()
    {
        ["IX_categories_slug"] = ("The slug is already used.", "category.slug_taken"),
        ["IX_products_slug"] = ("The slug is already used.", "product.slug_taken"),
        ["ux_product_images_one_primary_per_product"] =
            ("Another image became the primary one at the same moment. Reload and try again.", "image.primary_conflict"),
        ["IX_product_variants_sku"] = ("The SKU is already used.", "variant.sku_taken"),
        ["IX_cart_items_cart_id_variant_id"] =
            ("The variant was added to the cart at the same moment. Try again.", "cart.item_conflict"),
        ["IX_shipping_rates_method_code_max_weight_grams"] =
            ("The tariff was changed at the same moment. Reload it and try again.", "shipping.tariff_conflict"),
        ["ux_payments_one_open_payment_per_order"] =
            ("The order already has a payment in progress or paid.", "payment.already_open"),
        ["IX_payments_provider_provider_reference"] =
            ("The provider reference is already used.", "payment.reference_taken"),
        // The repeat of a message the provider is allowed to send; the payment service treats it as handled.
        ["IX_payment_events_provider_event_id"] = ("The message was already handled.", "payment.event_repeated"),
        // Backstop only: the order service looks for a free number first.
        ["IX_orders_number"] = ("The order number is taken. Try again.", "order.number_taken"),
    };

    private const string ChangedByOthers = "The record was changed by someone else. Reload it and try again.";

    private const string CartChangedAtTheSameMoment = "The cart was changed at the same moment. Open it and try again.";

    public static Exception? Translate(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException concurrency)
        {
            // A line of the cart that is already gone: the same cart was used twice at the same moment.
            if (concurrency.Entries.Any(entry => entry.Entity is CartItem))
            {
                return new ConflictException(CartChangedAtTheSameMoment, "cart.changed");
            }

            return ConcurrencyCode(concurrency) is { } code ? new ConflictException(ChangedByOthers, code) : null;
        }

        if (exception.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: { } name }
            && UniqueIndexes.TryGetValue(name, out var conflict))
        {
            return new ConflictException(conflict.Message, conflict.Code);
        }

        return null;
    }

    // The code comes from the type of the record that no longer matches. Only the types with a row version are listed.
    private static string? ConcurrencyCode(DbUpdateConcurrencyException exception)
    {
        var entity = exception.Entries.Select(entry => entry.Entity).FirstOrDefault();
        return entity switch
        {
            Category => "category.version_conflict",
            Product => "product.version_conflict",
            StoreSettings => "store_settings.version_conflict",
            Payment => "payment.version_conflict",
            Order => "order.version_conflict",
            _ => null,
        };
    }
}
