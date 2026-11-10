using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Api.Requests;

/// <summary>Changes the status of a product.</summary>
/// <param name="Status">Draft, Published or Archived.</param>
/// <param name="Version">The version of the product the caller last saw.</param>
public sealed record SetProductStatusRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ProductStatus Status,
    uint Version);
