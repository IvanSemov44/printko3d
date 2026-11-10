using System.Text.Json.Serialization;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>Moves a category one place among the categories with the same parent.</summary>
/// <param name="Direction">Up or Down.</param>
/// <param name="Version">The version of the moved category the caller last saw.</param>
public sealed record MoveCategoryRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MoveDirection Direction,
    uint Version);
