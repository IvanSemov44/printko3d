using System.Text.Json.Serialization;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>Moves a variant one place among the variants of its product.</summary>
/// <param name="Direction">Up or Down.</param>
public sealed record MoveVariantRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MoveDirection Direction);
