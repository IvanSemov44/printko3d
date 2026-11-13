namespace Storefront.Application.Orders;

/// <summary>Settings of placing an order. Bound from the <c>Checkout</c> section.</summary>
public sealed class OrderOptions
{
    public const string SectionName = "Checkout";

    /// <summary>
    /// The version of the terms and conditions written to every order. Until the legal pages exist (task 32)
    /// this is a placeholder, so it is clear which orders were accepted under which text.
    /// </summary>
    public string TermsVersion { get; set; } = "draft-1";
}
