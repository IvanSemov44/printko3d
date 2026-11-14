namespace Storefront.Api.Requests;

/// <summary>What a buyer types on the page "My order": the number and the email of the order.</summary>
public sealed record OrderLookupRequest(int Number, string Email);

/// <summary>The key from the link in the email.</summary>
public sealed record OrderTokenLookupRequest(string Token);
