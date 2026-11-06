using Storefront.Application.Catalog;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Seeding;

public sealed record PlannedCategory(string Name, string Slug, string Description, int SortOrder);

public sealed record PlannedVariant(
    string Sku,
    string Name,
    long Price,
    long? CompareAtPrice,
    int WeightGrams,
    int? StockQuantity,
    IReadOnlyDictionary<string, string> Attributes,
    int SortOrder);

public sealed record PlannedProduct(
    string CategorySlug,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    ProductStatus Status,
    IReadOnlyList<PlannedVariant> Variants);

public sealed record DemoCatalogPlan(IReadOnlyList<PlannedCategory> Categories, IReadOnlyList<PlannedProduct> Products);

/// <summary>
/// The fixed demo catalog. Names, slugs and SKUs are literals. The numbers (prices, weights, stock) come from a
/// <see cref="Random"/> with a fixed seed that is always drawn in the same order, so every run gives the same plan.
/// All text is obviously fake and Bulgarian: it is data, not UI text.
/// </summary>
public static class DemoCatalog
{
    public const int RandomSeed = 42;

    public static readonly NewStoreSettings Settings = new(
        StoreName: "Принтко 3Д",
        DefaultCurrency: "EUR",
        DefaultLocale: "bg",
        ContactEmail: "demo@example.com",
        ContactPhone: "+359 000 000 000",
        PrimaryColor: "#2563EB",
        IsDemo: true);

    private const string Puzzles = "3d-puzzles";
    private const string Figurines = "figurines";
    private const string Decor = "decor";
    private const string Gifts = "gifts";

    private static readonly Dictionary<string, string> Colors = new()
    {
        ["WHT"] = "Бял",
        ["BLK"] = "Черен",
        ["BLU"] = "Син",
        ["RED"] = "Червен",
        ["GRN"] = "Зелен",
    };

    private static readonly Dictionary<string, string> Sizes = new()
    {
        ["S"] = "Малка",
        ["L"] = "Голяма",
    };

    private static readonly string[] BothSizes = ["S", "L"];

    private static readonly PlannedCategory[] CategoryList =
    [
        new("3D пъзели", Puzzles, "Демо категория с фалшиви 3D пъзели.", 1),
        new("Фигурки", Figurines, "Демо категория с фалшиви фигурки.", 2),
        new("Декорация", Decor, "Демо категория с фалшива декорация.", 3),
        new("Подаръци", Gifts, "Демо категория с фалшиви подаръци.", 4),
    ];

    private static readonly Spec[] Specs =
    [
        new(Puzzles, "dragon-3d-puzzle", "Демо 3D пъзел „Дракон“", "PZL-DRAGON", "Фалшив пъзел за проба.", FulfilmentType.MadeToOrder, 7, ProductStatus.Published, ["WHT", "BLK", "BLU"], []),
        new(Puzzles, "castle-3d-puzzle", "Демо 3D пъзел „Замък“", "PZL-CASTLE", "Фалшив замък за проверка на каталога.", FulfilmentType.MadeToOrder, 10, ProductStatus.Published, ["WHT", "RED"], []),
        new(Puzzles, "tower-3d-puzzle", "Демо 3D пъзел „Кула“", "PZL-TOWER", "Фалшива кула, не се продава.", FulfilmentType.MadeToOrder, 5, ProductStatus.Published, ["BLK", "BLU", "GRN"], []),
        new(Puzzles, "ship-3d-puzzle", "Демо 3D пъзел „Кораб“", "PZL-SHIP", "Фалшив кораб, архивиран за проба.", FulfilmentType.MadeToOrder, 8, ProductStatus.Archived, ["WHT", "BLU"], []),
        new(Puzzles, "globe-3d-puzzle", "Демо 3D пъзел „Глобус“", "PZL-GLOBE", "Фалшив глобус, налично количество.", FulfilmentType.InStock, null, ProductStatus.Published, ["WHT", "GRN"], []),
        new(Figurines, "owl-figurine", "Демо фигурка „Бухал“", "FIG-OWL", "Фалшива фигурка за проба.", FulfilmentType.InStock, null, ProductStatus.Published, ["WHT", "BLK"], BothSizes),
        new(Figurines, "fox-figurine", "Демо фигурка „Лисица“", "FIG-FOX", "Фалшива лисица за проверка на каталога.", FulfilmentType.InStock, null, ProductStatus.Published, ["RED", "WHT"], BothSizes),
        new(Figurines, "robot-figurine", "Демо фигурка „Робот“", "FIG-ROBOT", "Фалшив робот, прави се по поръчка.", FulfilmentType.MadeToOrder, 6, ProductStatus.Published, ["BLU", "BLK"], BothSizes),
        new(Figurines, "dinosaur-figurine", "Демо фигурка „Динозавър“", "FIG-DINO", "Фалшив динозавър, не е истински артикул.", FulfilmentType.InStock, null, ProductStatus.Published, ["GRN"], BothSizes),
        new(Figurines, "knight-figurine", "Демо фигурка „Рицар“", "FIG-KNIGHT", "Фалшив рицар, остава чернова.", FulfilmentType.MadeToOrder, 9, ProductStatus.Draft, ["WHT"], BothSizes),
        new(Decor, "vase-decor", "Демо ваза", "DEC-VASE", "Фалшива ваза за проба.", FulfilmentType.InStock, null, ProductStatus.Published, [], []),
        new(Decor, "lampshade-decor", "Демо абажур", "DEC-LAMP", "Фалшив абажур, изчерпана наличност.", FulfilmentType.InStock, null, ProductStatus.Published, [], [], ZeroStock: true),
        new(Decor, "planter-decor", "Демо саксия", "DEC-PLANTER", "Фалшива саксия за проверка на каталога.", FulfilmentType.InStock, null, ProductStatus.Published, [], []),
        new(Gifts, "keychain-gift", "Демо ключодържател", "GFT-KEYCHAIN", "Фалшив ключодържател за проба.", FulfilmentType.InStock, null, ProductStatus.Published, [], []),
        new(Gifts, "pen-holder-gift", "Демо поставка за химикали", "GFT-PENHOLDER", "Фалшива поставка, не се продава.", FulfilmentType.InStock, null, ProductStatus.Published, [], []),
        new(Gifts, "phone-stand-gift", "Демо стойка за телефон", "GFT-PHONESTAND", "Фалшива стойка за проверка на каталога.", FulfilmentType.InStock, null, ProductStatus.Published, [], []),
    ];

    public static DemoCatalogPlan Build()
    {
        var random = new Random(RandomSeed);
        var products = Specs.Select(spec => BuildProduct(spec, random)).ToList();
        return new DemoCatalogPlan(CategoryList, products);
    }

    private static PlannedProduct BuildProduct(Spec spec, Random random)
    {
        var basePrice = random.Next(16, 241) * 50L;
        var variants = new List<PlannedVariant>();
        var sortOrder = 0;
        var colorCodes = OrNone(spec.Colors);
        var sizeCodes = OrNone(spec.Sizes);
        foreach (var color in colorCodes)
        {
            foreach (var size in sizeCodes)
            {
                variants.Add(BuildVariant(spec, color, size, basePrice, sortOrder++, random));
            }
        }

        return new PlannedProduct(
            spec.Category,
            spec.Name,
            spec.Slug,
            $"{spec.Phrase} Демо данни.",
            $"{spec.Name} е ясно фалшив продукт, създаден само за демонстрация. {spec.Phrase} "
            + "Не е истински артикул и не може да бъде поръчан.",
            spec.Type,
            spec.Lead,
            spec.Status,
            variants);
    }

    private static PlannedVariant BuildVariant(
        Spec spec, string? color, string? size, long basePrice, int sortOrder, Random random)
    {
        var price = basePrice + (size == "L" ? RoundDown(basePrice / 2) : 0);
        var weight = random.Next(30, 601);
        var stockDraw = random.Next(1, 41);
        var compareDraw = random.Next(5);

        var attributes = new Dictionary<string, string>();
        var nameParts = new List<string>();
        var sku = spec.Sku;
        if (color is not null)
        {
            attributes["color"] = Colors[color];
            nameParts.Add(Colors[color]);
            sku += "-" + color;
        }

        if (size is not null)
        {
            attributes["size"] = Sizes[size];
            nameParts.Add(Sizes[size].ToLowerInvariant());
            sku += "-" + size;
        }

        var stock = spec.Type == FulfilmentType.MadeToOrder ? (int?)null : spec.ZeroStock ? 0 : stockDraw;
        long? compareAt = compareDraw == 0 ? price + Math.Max(50, RoundDown(price / 5)) : null;
        return new PlannedVariant(
            sku,
            nameParts.Count > 0 ? string.Join(", ", nameParts) : "Стандартен",
            price,
            compareAt,
            weight,
            stock,
            attributes,
            sortOrder);
    }

    private static string?[] OrNone(string[] codes) => codes.Length > 0 ? [.. codes] : [null];

    private static long RoundDown(long value) => value / 50 * 50;

    private sealed record Spec(
        string Category,
        string Slug,
        string Name,
        string Sku,
        string Phrase,
        FulfilmentType Type,
        int? Lead,
        ProductStatus Status,
        string[] Colors,
        string[] Sizes,
        bool ZeroStock = false);
}
