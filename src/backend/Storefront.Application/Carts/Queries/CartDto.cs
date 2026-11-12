namespace Storefront.Application.Carts.Queries;

/// <summary>
/// <paramref name="ItemCount"/> counts the units in every line. <paramref name="Total"/> adds up only the
/// lines that are <see cref="CartLineState.Available"/>, so the shopper is never charged for a line that cannot be ordered.
/// </summary>
public sealed record CartDto(IReadOnlyList<CartLineDto> Lines, int ItemCount, long Total)
{
    public static CartDto Empty { get; } = new([], 0, 0);

    public static CartDto From(IReadOnlyList<CartLineDto> lines) =>
        new(
            lines,
            lines.Sum(l => l.Quantity),
            lines.Where(l => l.State == CartLineState.Available).Sum(l => l.LinePrice));
}
