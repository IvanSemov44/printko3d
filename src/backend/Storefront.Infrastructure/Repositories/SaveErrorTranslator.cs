using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

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
        ["IX_product_variants_sku"] = ("The SKU is already used.", "variant.sku_taken"),
    };

    private const string ChangedByOthers = "The record was changed by someone else. Reload it and try again.";

    public static Exception? Translate(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException concurrency)
        {
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
            _ => null,
        };
    }
}
