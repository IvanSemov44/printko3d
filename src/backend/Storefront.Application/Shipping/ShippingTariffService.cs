using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Shipping;

namespace Storefront.Application.Shipping;

internal sealed class ShippingTariffService(IRepositoryManager repositories) : IShippingTariffService
{
    public const int MaxSteps = 20;

    public const int MaxWeightGrams = 1_000_000;

    public const long MaxPrice = 100_000_000;

    public async Task<ShippingTariffDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await repositories.ShippingRates.ListAsync(ShippingMethods.Address, cancellationToken));

    public async Task<ShippingTariffDto> ReplaceAsync(
        IReadOnlyList<ShippingTariffStep> steps,
        CancellationToken cancellationToken)
    {
        Validate(steps);

        // The old steps go and the new ones come in one transaction, so the shop is never left without a tariff in between.
        await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);
        await repositories.ShippingRates.DeleteAllAsync(ShippingMethods.Address, cancellationToken);
        foreach (var step in steps)
        {
            repositories.ShippingRates.Create(new ShippingRate
            {
                MethodCode = ShippingMethods.Address,
                MaxWeightGrams = step.MaxWeightGrams,
                Price = step.Price,
            });
        }

        await repositories.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static ShippingTariffDto ToDto(IReadOnlyList<ShippingRate> rates) =>
        new(rates.Select(r => new ShippingTariffStep(r.MaxWeightGrams, r.Price)).ToList());

    private static void Validate(IReadOnlyList<ShippingTariffStep> steps)
    {
        var errors = new Dictionary<string, string[]>();
        if (steps.Count > MaxSteps)
        {
            errors["steps"] = [$"At most {MaxSteps} steps are allowed."];
        }

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].MaxWeightGrams is < 1 or > MaxWeightGrams)
            {
                errors[$"steps[{i}].maxWeightGrams"] = [$"The weight is between 1 and {MaxWeightGrams} grams."];
            }

            if (steps[i].Price is < 0 or > MaxPrice)
            {
                errors[$"steps[{i}].price"] = [$"The price is between 0 and {MaxPrice}."];
            }
        }

        // Two steps of one weight would leave the price to the order of reading; the database refuses them too.
        var repeated = steps.GroupBy(s => s.MaxWeightGrams).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repeated.Count > 0 && !errors.ContainsKey("steps"))
        {
            errors["steps"] = ["Two steps have the same weight."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("The tariff has invalid steps.", errors);
        }
    }
}
