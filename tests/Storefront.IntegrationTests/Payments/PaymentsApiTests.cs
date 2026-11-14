using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Storefront.Application.Payments;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Auth;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Payments;

/// <summary>The payment addresses over HTTP (task 26c2): the whole road from the order to a paid order.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PaymentsApiTests(PostgresFixture postgres)
{
    private const string CartCookieName = "__Host-printko-cart";
    private const string NotificationsUrl = "/api/v1/payments/notifications";

    // Made-up personal data; the journal test looks for every one of them.
    private const string BuyerName = "Zlatko Testov";
    private const string BuyerEmail = "zlatko.testov@example.test";
    private const string BuyerPhone = "+359 88 123 4567";
    private const string BuyerStreet = "12 Imaginary Street";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _entries = [];

        public string AllText()
        {
            lock (_entries)
            {
                return string.Join('\n', _entries);
            }
        }

        public ILogger CreateLogger(string categoryName) => new Capturing(this);

        public void Dispose()
        {
        }

        private sealed class Capturing(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._entries)
                {
                    owner._entries.Add($"{formatter(state, exception)} {exception}");
                }
            }
        }
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task SeedAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(category.Id);
        product.Status = ProductStatus.Published;
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = 100;
        context.Add(category);
        context.Add(product);
        context.Add(CatalogData.Settings());
        context.Add(CatalogData.FreeShipping());
        await context.SaveChangesAsync();
        VariantId = variant.Id;
    }

    private static Guid VariantId { get; set; }

    /// <summary>Puts two units (3000) in a cart and orders them. Returns the number of the order.</summary>
    private static async Task<int> PlaceOrderAsync(AuthApiHost host)
    {
        var added = await host.SendAsync(HttpMethod.Post, "/api/v1/cart/items", new { variantId = VariantId, quantity = 2 });
        var cookie = added.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith(CartCookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        var placed = await host.SendAsync(
            HttpMethod.Post,
            "/api/v1/orders",
            new
            {
                name = BuyerName,
                email = BuyerEmail,
                phone = BuyerPhone,
                street = BuyerStreet,
                city = "Sofia",
                postalCode = "1000",
                termsAccepted = true,
                shippingMethod = "address",
                expectedTotal = 3000,
            },
            cookie);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        return (await JsonOf(placed)).GetProperty("number").GetInt32();
    }

    private static async Task<(Guid PaymentId, string Reference)> StartPaymentAsync(AuthApiHost host, int number)
    {
        var response = await host.SendAsync(HttpMethod.Put, $"/api/v1/orders/{number}/payment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        var redirect = body.GetProperty("redirectUrl").GetString()!;
        return (body.GetProperty("paymentId").GetGuid(), redirect["/pay/test/".Length..]);
    }

    private static async Task<HttpResponseMessage> PostNotificationAsync(AuthApiHost host, PaymentMessage message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, NotificationsUrl)
        {
            Content = new StringContent(message.Body, Encoding.UTF8, "application/json"),
        };
        if (message.Signature is not null)
        {
            request.Headers.Add("X-Payment-Signature", message.Signature);
        }

        // No X-Requested-With: the provider cannot send it (decision 0035).
        return await host.Client.SendAsync(request);
    }

    private static async Task<OrderStatus> StatusOfAsync(AuthApiHost host, int number)
    {
        await using var context = host.Database.CreateContext();
        return await context.Orders.AsNoTracking().Where(o => o.Number == number).Select(o => o.Status).SingleAsync();
    }

    private async Task<AuthApiHost> CreateHostAsync(
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var host = await AuthApiHost.CreateAsync(
            postgres, createAdmin: false, configureServices: configureServices, extraSettings: settings);
        await SeedAsync(host);
        return host;
    }

    private static Task<PaymentMessage> MessageAsync(AuthApiHost host, string reference, PaymentOutcome outcome, long amount = 3000) =>
        host.Services.GetRequiredService<IPaymentSimulator>().CreateMessageAsync(reference, outcome, amount, null, default);

    [Fact]
    public async Task TheWholeRoad_FromTheOrderToAPaidOrder_ThroughTheTestPage()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);

        var (paymentId, reference) = await StartPaymentAsync(host, number);
        var pending = await JsonOf(await host.SendAsync(HttpMethod.Get, $"/api/v1/payments/{paymentId}"));
        var completed = await host.SendAsync(
            HttpMethod.Post, $"/api/v1/payments/test/{reference}/complete", new { outcome = "Paid" });
        var paid = await JsonOf(await host.SendAsync(HttpMethod.Get, $"/api/v1/payments/{paymentId}"));

        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Equal("Succeeded", paid.GetProperty("status").GetString());
        Assert.Equal(OrderStatus.Paid, await StatusOfAsync(host, number));
    }

    [Fact]
    public async Task TheStatus_CarriesOnlyTheState_NothingAboutTheBuyerOrTheOrder()
    {
        await using var host = await CreateHostAsync();
        var (paymentId, _) = await StartPaymentAsync(host, await PlaceOrderAsync(host));

        var body = await JsonOf(await host.SendAsync(HttpMethod.Get, $"/api/v1/payments/{paymentId}"));

        Assert.Equal(["status"], body.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task TheSession_AnswersWithTheRedirectAndTheDeadline_AndAsksForTheCsrfHeader()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);

        var without = await host.SendAsync(HttpMethod.Put, $"/api/v1/orders/{number}/payment", csrfHeader: false);
        var response = await host.SendAsync(HttpMethod.Put, $"/api/v1/orders/{number}/payment");
        var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Forbidden, without.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("/pay/test/test_", body.GetProperty("redirectUrl").GetString(), StringComparison.Ordinal);
        Assert.True(body.TryGetProperty("expiresAt", out _));
    }

    [Fact]
    public async Task TheSession_OfAnUnknownOrder_Is404_AndOfAPaidOrderIs409()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);
        var (_, reference) = await StartPaymentAsync(host, number);
        await host.SendAsync(HttpMethod.Post, $"/api/v1/payments/test/{reference}/complete", new { outcome = "Paid" });

        var unknown = await host.SendAsync(HttpMethod.Put, "/api/v1/orders/99999999/payment");
        var paid = await host.SendAsync(HttpMethod.Put, $"/api/v1/orders/{number}/payment");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("order.not_found", (await JsonOf(unknown)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, paid.StatusCode);
        Assert.Equal("payment.order_not_payable", (await JsonOf(paid)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheStatus_OfAnUnknownPayment_Is404()
    {
        await using var host = await CreateHostAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("payment.not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheTestPage_WithAnOutcomeThatIsNotKnown_Is400()
    {
        await using var host = await CreateHostAsync();
        var (_, reference) = await StartPaymentAsync(host, await PlaceOrderAsync(host));

        var response = await host.SendAsync(
            HttpMethod.Post, $"/api/v1/payments/test/{reference}/complete", new { outcome = "Refunded" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheProviderAddress_TakesASignedMessageWithoutTheCsrfHeader_AndAnswersARepeatTheSame()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);
        var (_, reference) = await StartPaymentAsync(host, number);
        var message = await MessageAsync(host, reference, PaymentOutcome.Paid);

        var first = await PostNotificationAsync(host, message);
        var repeat = await PostNotificationAsync(host, message);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeat.StatusCode);
        Assert.Equal(OrderStatus.Paid, await StatusOfAsync(host, number));
    }

    [Fact]
    public async Task TheProviderAddress_RefusesAWrongOrMissingSignature_WithoutSayingWhy()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);
        var (_, reference) = await StartPaymentAsync(host, number);
        var message = await MessageAsync(host, reference, PaymentOutcome.Paid);

        var wrong = await PostNotificationAsync(host, message with { Signature = "t=1,v1=00" });
        var missing = await PostNotificationAsync(host, message with { Signature = null });
        var changed = await PostNotificationAsync(host, message with { Body = message.Body.Replace("3000", "1", StringComparison.Ordinal) });

        foreach (var response in new[] { wrong, missing, changed })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.Equal("payment.message_rejected", JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("signature", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(reference, text, StringComparison.Ordinal);
        }

        Assert.Equal(OrderStatus.New, await StatusOfAsync(host, number));
    }

    [Fact]
    public async Task TheProviderAddress_RefusesABodyThatIsTooLarge()
    {
        await using var host = await CreateHostAsync();

        var response = await PostNotificationAsync(host, new PaymentMessage(new string('x', 20 * 1024), "t=1,v1=00"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task TheProviderAddress_IsTheOnlyOneWithoutTheCsrfHeader()
    {
        await using var host = await CreateHostAsync();
        var number = await PlaceOrderAsync(host);

        var other = await host.SendAsync(HttpMethod.Post, "/api/v1/payments/test/anything/complete", new { outcome = "Paid" }, csrfHeader: false);
        var anotherPath = await host.SendAsync(HttpMethod.Post, NotificationsUrl + "/extra", new { }, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, anotherPath.StatusCode);
        Assert.Equal(OrderStatus.New, await StatusOfAsync(host, number));
    }

    [Fact]
    public async Task WhenPaymentIsTurnedOff_TheAddressesAnswer404()
    {
        await using var host = await CreateHostAsync(
            settings: new Dictionary<string, string> { ["Payment:Enabled"] = "false" });

        var start = await host.SendAsync(HttpMethod.Put, "/api/v1/orders/12345678/payment");
        var message = await MessageAsync(host, "test_9999999999_x", PaymentOutcome.Paid);
        var notification = await PostNotificationAsync(host, message);

        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Equal("payment.disabled", (await JsonOf(start)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, notification.StatusCode);
    }

    [Fact]
    public async Task TheJournal_NeverHoldsTheBuyerOrTheSignature_AfterPaymentsAndRefusals()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await CreateHostAsync(
            services => services.AddLogging(builder => builder.AddProvider(logs)));
        var number = await PlaceOrderAsync(host);
        var (_, reference) = await StartPaymentAsync(host, number);
        var message = await MessageAsync(host, reference, PaymentOutcome.Paid);

        await PostNotificationAsync(host, message with { Signature = "t=1,v1=deadbeef" });
        await PostNotificationAsync(host, message with { Body = "{\"reference\":\"" + reference + "\"}" });
        await PostNotificationAsync(host, message);
        await PostNotificationAsync(host, await MessageAsync(host, "test_0_unknown", PaymentOutcome.Failed));

        var text = logs.AllText();
        Assert.NotEmpty(text);
        foreach (var secret in new[] { BuyerName, BuyerEmail, BuyerPhone, BuyerStreet, "deadbeef", message.Signature! })
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }
}
