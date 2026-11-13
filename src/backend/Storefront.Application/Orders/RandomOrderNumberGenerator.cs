using System.Security.Cryptography;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>A random eight-digit number from a cryptographic generator. It says nothing about how many orders there are.</summary>
public sealed class RandomOrderNumberGenerator : IOrderNumberGenerator
{
    public int Next() => RandomNumberGenerator.GetInt32(Order.MinNumber, Order.MaxNumber + 1);
}
