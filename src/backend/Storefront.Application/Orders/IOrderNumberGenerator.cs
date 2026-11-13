namespace Storefront.Application.Orders;

/// <summary>Gives the number a new order is shown with (decision 0032). Behind an interface so a test can force a clash.</summary>
public interface IOrderNumberGenerator
{
    int Next();
}
