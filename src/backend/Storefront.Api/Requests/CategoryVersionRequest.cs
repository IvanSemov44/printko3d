namespace Storefront.Api.Requests;

/// <summary>The version the caller last saw. A different value means someone else saved first.</summary>
public sealed record CategoryVersionRequest(uint Version);
