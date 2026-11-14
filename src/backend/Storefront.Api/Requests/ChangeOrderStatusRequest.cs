using System.Text.Json.Serialization;
using Storefront.Domain.Orders;

namespace Storefront.Api.Requests;

/// <summary>The production step the administrator takes.</summary>
/// <param name="Status">InProduction, ReadyToShip, Shipped or Completed, the one after the current status.</param>
public sealed record ChangeOrderStatusRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OrderStatus Status);
