using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Domain.Carts;
using Storefront.Domain.Orders;
using Storefront.Infrastructure.Identity;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class CustomerConstraintTests(PostgresFixture postgres)
{
    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    private static CustomerUser NewCustomer(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = "Test",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Cart NewCart(Guid? customerId) =>
        new() { TokenHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), CustomerId = customerId };

    [Fact]
    public async Task Customer_RejectsTheSameEmailTwice()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            context.Add(NewCustomer("a@example.test"));
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(NewCustomer("a@example.test"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));
    }

    [Fact]
    public async Task ExternalLogin_RejectsTheSameProviderKeyTwice_AndAcceptsAnotherProvider()
    {
        var database = await postgres.CreateDatabaseAsync();
        var first = NewCustomer("a@example.test");
        var second = NewCustomer("b@example.test");
        await using (var context = database.CreateContext())
        {
            context.AddRange(first, second);
            context.Add(new CustomerExternalLogin { Id = Guid.NewGuid(), CustomerId = first.Id, Provider = "Google", ProviderKey = "k1" });
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(new CustomerExternalLogin { Id = Guid.NewGuid(), CustomerId = second.Id, Provider = "Google", ProviderKey = "k1" });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var other = database.CreateContext();
        other.Add(new CustomerExternalLogin { Id = Guid.NewGuid(), CustomerId = second.Id, Provider = "Facebook", ProviderKey = "k1" });
        Assert.Equal(1, await other.SaveChangesAsync());
    }

    [Fact]
    public async Task ExternalLogin_RejectsAnUnknownCustomer()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(new CustomerExternalLogin { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Provider = "Google", ProviderKey = "k1" });

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Cart_AllowsOneCartPerCustomer_AndManyAnonymousCarts()
    {
        var database = await postgres.CreateDatabaseAsync();
        var customer = NewCustomer("a@example.test");
        await using (var context = database.CreateContext())
        {
            context.Add(customer);
            context.Add(NewCart(customer.Id));
            context.Add(NewCart(null));
            context.Add(NewCart(null));
            await context.SaveChangesAsync();
        }

        await using var second = database.CreateContext();
        second.Add(NewCart(customer.Id));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => second.SaveChangesAsync()));
    }

    [Fact]
    public async Task Cart_IsDeletedWithItsCustomer()
    {
        var database = await postgres.CreateDatabaseAsync();
        var customer = NewCustomer("a@example.test");
        await using (var context = database.CreateContext())
        {
            context.Add(customer);
            context.Add(NewCart(customer.Id));
            await context.SaveChangesAsync();
        }

        await using var delete = database.CreateContext();
        await delete.CustomerUsers.Where(u => u.Id == customer.Id).ExecuteDeleteAsync();

        Assert.Equal(0, await delete.Carts.CountAsync(c => c.CustomerId == customer.Id));
    }

    [Fact]
    public async Task Order_KeepsItself_WhenItsCustomerIsDeleted()
    {
        var database = await postgres.CreateDatabaseAsync();
        var customer = NewCustomer("a@example.test");
        var orderId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            context.Add(customer);
            context.Add(new Order
            {
                Id = orderId,
                Number = 12345678,
                Currency = "BGN",
                TotalAmount = 100,
                ShippingMethod = "test",
                CustomerName = "Test",
                Email = "a@example.test",
                Phone = "0000",
                ShippingStreet = "Street 1",
                ShippingCity = "City",
                ShippingPostalCode = "1000",
                ShippingCountry = "BG",
                TermsVersion = "1",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                CustomerId = customer.Id,
            });
            await context.SaveChangesAsync();
        }

        await using var delete = database.CreateContext();
        await delete.CustomerUsers.Where(u => u.Id == customer.Id).ExecuteDeleteAsync();

        var order = await delete.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Null(order.CustomerId);
    }
}
