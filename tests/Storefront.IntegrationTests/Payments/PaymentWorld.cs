using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Payments;

/// <summary>The database, the API services and a fake clock that the payment tests share (task 26c).</summary>
internal static class PaymentWorld
{
    public static readonly Guid FirstVariantId = new("00000000-0000-0000-0000-0000000000a1");

    public static readonly Guid SecondVariantId = new("00000000-0000-0000-0000-0000000000a2");

    public sealed record Setup(TestDatabase Database, WebApplicationFactory<Program> Factory, FakeTimeProvider Time)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    public static PlaceOrderInput Input(long expectedTotal) =>
        new("Test Buyer", "buyer@example.com", "+359 88 000 0000", "1 Test Street", "Sofia", "1000", true, "address", expectedTotal);

    public static async Task<Setup> CreateAsync(PostgresFixture postgres, int firstStock = 5, int secondStock = 5, bool paymentEnabled = true)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            var category = CatalogData.Category();
            context.Add(category);
            context.Add(CatalogData.Settings());
            context.Add(CatalogData.FreeShipping());
            foreach (var (slug, sku, id, stock) in new[]
            {
                ("dragon", "DRG-001", FirstVariantId, firstStock),
                ("knight", "KNT-001", SecondVariantId, secondStock),
            })
            {
                var product = CatalogData.Product(category.Id, slug);
                product.Status = ProductStatus.Published;
                var variant = CatalogData.Variant(product, sku);
                variant.Id = id;
                variant.StockQuantity = stock;
                context.Add(product);
            }

            await context.SaveChangesAsync();
        }

        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = new ApiFactory(database.ConnectionString).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payment:Enabled", paymentEnabled ? "true" : "false");
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(time));
        });
        return new Setup(database, factory, time);
    }

    public static async Task<T> InScopeAsync<T>(Setup setup, Func<IServiceManager, Task<T>> action)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IServiceManager>());
    }

    /// <summary>An order of two pieces of the first variant (3000 in all), and of one piece of the second when asked.</summary>
    public static async Task<OrderDto> PlaceAsync(Setup setup, bool withSecond = false)
    {
        string? token = null;
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(token, FirstVariantId, 2, default));
        token = change.Token;
        var total = 3000L;
        if (withSecond)
        {
            await InScopeAsync(setup, s => s.Cart.AddItemAsync(token, SecondVariantId, 1, default));
            total += 1500;
        }

        return await InScopeAsync(setup, s => s.Orders.PlaceAsync(token, Input(total), default));
    }

    public static Task<PaymentSessionDto> StartAsync(Setup setup, int number) =>
        InScopeAsync(setup, s => s.Payments.StartAsync(number, default));

    public static async Task<string> ReferenceOfAsync(Setup setup, Guid paymentId)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Payments.AsNoTracking()
            .Where(p => p.PublicId == paymentId).Select(p => p.ProviderReference).SingleAsync();
    }

    public static async Task<PaymentMessage> MessageAsync(
        Setup setup,
        string reference,
        PaymentOutcome outcome,
        long amount = 3000,
        string? eventId = null)
    {
        var simulator = setup.Factory.Services.GetRequiredService<IPaymentSimulator>();
        return await simulator.CreateMessageAsync(reference, outcome, amount, eventId, default);
    }

    public static async Task<PaymentNotificationResult> SendAsync(Setup setup, PaymentMessage message) =>
        (await InScopeAsync(setup, s => s.Payments.HandleNotificationAsync(message, default))).Result;

    /// <summary>Starts a payment and returns the session with the reference the provider gave it.</summary>
    public static async Task<(PaymentSessionDto Session, string Reference)> OpenAsync(Setup setup, OrderDto order)
    {
        var session = await StartAsync(setup, order.Number);
        return (session, await ReferenceOfAsync(setup, session.PaymentId));
    }

    public static async Task<int?> StockOfAsync(Setup setup, Guid variantId)
    {
        await using var context = setup.Database.CreateContext();
        return await context.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.StockQuantity).SingleAsync();
    }

    public static async Task<Order> OrderAsync(Setup setup, int number)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Orders.AsNoTracking().SingleAsync(o => o.Number == number);
    }

    public static async Task<List<Payment>> PaymentsAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Payments.AsNoTracking().OrderBy(p => p.CreatedAt).ToListAsync();
    }

    public static async Task<int> EventCountAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.PaymentEvents.CountAsync();
    }

    // What the expiry of task 26d does, written by hand: the order is cancelled and its stock goes back.
    public static async Task CancelAsync(Setup setup, OrderDto order)
    {
        var current = await OrderAsync(setup, order.Number);
        await using (var context = setup.Database.CreateContext())
        {
            await context.Orders.Where(o => o.Id == current.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled));
        }

        await InScopeAsync(setup, async s =>
        {
            await s.Orders.ReleaseStockAsync(current.Id, default);
            return true;
        });
    }
}
