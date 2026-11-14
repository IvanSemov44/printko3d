using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class PaymentConstraintTests(PostgresFixture postgres)
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    private static Order NewOrder(int number = 12345678) => new()
    {
        Number = number,
        Currency = "BGN",
        TotalAmount = 3000,
        CustomerName = "Test Buyer",
        Email = "buyer@example.com",
        Phone = "+359000000",
        ShippingStreet = "1 Test Street",
        ShippingCity = "Sofia",
        ShippingPostalCode = "1000",
        ShippingCountry = "BG",
        TermsVersion = "draft-1",
        TermsAcceptedAt = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero),
    };

    private static Payment NewPayment(
        Guid orderId,
        string reference = "ref-1",
        PaymentStatus status = PaymentStatus.Pending,
        string provider = "test") => new()
        {
            OrderId = orderId,
            Provider = provider,
            ProviderReference = reference,
            Amount = 3000,
            Currency = "BGN",
            Status = status,
        };

    private static async Task<Guid> SeedOrderAsync(TestDatabase database, int number = 12345678)
    {
        await using var context = database.CreateContext();
        var order = NewOrder(number);
        context.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }

    private static async Task SeedPaymentAsync(TestDatabase database, Payment payment)
    {
        await using var context = database.CreateContext();
        context.Add(payment);
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Payment_RejectsANonPositiveAmount(long amount)
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        var payment = NewPayment(orderId);
        payment.Amount = amount;
        context.Add(payment);

        Assert.Equal(CheckViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Payment_RejectsACurrencyThatIsNotThreeLetters()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        var payment = NewPayment(orderId);
        payment.Currency = "LEV";
        context.Add(payment);
        await context.SaveChangesAsync();

        await using var wrong = database.CreateContext();
        var other = NewPayment(await SeedOrderAsync(database, 12345679), "ref-2");
        other.Currency = "BG";
        wrong.Add(other);

        Assert.Equal(CheckViolation, await SqlStateOfAsync(() => wrong.SaveChangesAsync()));
    }

    [Fact]
    public async Task Payment_RejectsAnOrderThatDoesNotExist()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(NewPayment(Guid.NewGuid()));

        Assert.Equal(ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Payment_RejectsADuplicateReference_AndAcceptsTheSameReferenceOfAnotherProvider()
    {
        var database = await postgres.CreateDatabaseAsync();
        var first = await SeedOrderAsync(database);
        var second = await SeedOrderAsync(database, 12345679);
        await SeedPaymentAsync(database, NewPayment(first));

        await using var duplicate = database.CreateContext();
        duplicate.Add(NewPayment(second));
        Assert.Equal(UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var other = database.CreateContext();
        other.Add(NewPayment(second, provider: "other"));
        await other.SaveChangesAsync();
    }

    [Fact]
    public async Task Payment_RejectsADuplicatePublicId()
    {
        var database = await postgres.CreateDatabaseAsync();
        var first = await SeedOrderAsync(database);
        var second = await SeedOrderAsync(database, 12345679);
        var original = NewPayment(first);
        await SeedPaymentAsync(database, original);

        await using var context = database.CreateContext();
        var copy = NewPayment(second, "ref-2");
        copy.PublicId = original.PublicId;
        context.Add(copy);

        Assert.Equal(UniqueViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.PaidWithoutOrder)]
    public async Task Payment_RejectsASecondOpenPaymentForTheSameOrder(PaymentStatus existing)
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        await SeedPaymentAsync(database, NewPayment(orderId, "ref-1", existing));

        await using var context = database.CreateContext();
        context.Add(NewPayment(orderId, "ref-2"));

        Assert.Equal(UniqueViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Payment_AcceptsANewTryAfterAFailedOne_AndSeveralFailedOnes()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        await SeedPaymentAsync(database, NewPayment(orderId, "ref-1", PaymentStatus.Failed));
        await SeedPaymentAsync(database, NewPayment(orderId, "ref-2", PaymentStatus.Failed));

        await SeedPaymentAsync(database, NewPayment(orderId, "ref-3"));
    }

    [Fact]
    public async Task PaymentEvent_RejectsARepeatedMessage_AndAcceptsTheSameNumberFromAnotherProvider()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        var payment = NewPayment(orderId);
        await SeedPaymentAsync(database, payment);
        await using (var context = database.CreateContext())
        {
            context.Add(new PaymentEvent { PaymentId = payment.Id, Provider = "test", EventId = "evt-1" });
            await context.SaveChangesAsync();
        }

        await using var repeated = database.CreateContext();
        repeated.Add(new PaymentEvent { PaymentId = payment.Id, Provider = "test", EventId = "evt-1" });
        Assert.Equal(UniqueViolation, await SqlStateOfAsync(() => repeated.SaveChangesAsync()));

        await using var other = database.CreateContext();
        other.Add(new PaymentEvent { PaymentId = payment.Id, Provider = "other", EventId = "evt-1" });
        await other.SaveChangesAsync();
    }

    [Fact]
    public async Task PaymentEvent_RejectsAPaymentThatDoesNotExist()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(new PaymentEvent { PaymentId = Guid.NewGuid(), Provider = "test", EventId = "evt-1" });

        Assert.Equal(ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Order_AcceptsThePaidAndCancelledStatuses(OrderStatus status)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var order = NewOrder();
        order.Status = status;
        order.PaymentDueAt = new DateTimeOffset(2026, 11, 14, 11, 0, 0, TimeSpan.Zero);
        context.Add(order);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Order_RejectsAStatusThatTheCodeDoesNotKnow()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();

        Assert.Equal(
            CheckViolation,
            await SqlStateOfAsync(() => context.Database.ExecuteSqlAsync(
                $"UPDATE orders SET status = 'Shipped' WHERE id = {orderId}")));
    }
}
