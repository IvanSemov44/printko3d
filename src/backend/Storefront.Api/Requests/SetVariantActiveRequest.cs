namespace Storefront.Api.Requests;

/// <summary>Shows or hides one variant in the shop.</summary>
public sealed record SetVariantActiveRequest(bool IsActive);
