using System.Text.Json.Serialization;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>Moves an image one place among the images of its product.</summary>
/// <param name="Direction">Up or Down.</param>
public sealed record MoveImageRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MoveDirection Direction);
