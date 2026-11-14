using System.Globalization;
using Storefront.Application.Contracts;
using Storefront.Domain.Orders;

namespace Storefront.Application.Emails;

/// <summary>Builds the emails a buyer gets about an order (decision 0036). Plain text; the wording is in <c>Texts/bg.json</c>.</summary>
internal static class OrderEmails
{
    public static EmailMessage Placed(Order order, string storefrontUrl)
    {
        var texts = EmailTexts.Default;
        var lines = string.Join('\n', order.Items.Select(item => EmailTexts.Fill(texts.Text("lineFormat"), new Dictionary<string, string>
        {
            ["product"] = item.ProductName,
            ["variant"] = item.VariantName,
            ["quantity"] = item.Quantity.ToString(CultureInfo.InvariantCulture),
            ["total"] = Money(item.LineTotal, order.Currency),
        })));
        var shipping = order.ShippingAmount == 0 ? texts.Text("freeShipping") : Money(order.ShippingAmount, order.Currency);
        return Build("orderPlaced", order, storefrontUrl, new Dictionary<string, string>
        {
            ["lines"] = lines,
            ["shipping"] = shipping,
        });
    }

    public static EmailMessage Paid(Order order, string storefrontUrl) => Build("orderPaid", order, storefrontUrl, []);

    public static EmailMessage Shipped(Order order, string storefrontUrl) => Build("orderShipped", order, storefrontUrl, new Dictionary<string, string>
    {
        ["address"] = $"{order.ShippingStreet}, {order.ShippingPostalCode} {order.ShippingCity}",
    });

    /// <summary>
    /// The email to the administrator about a new order (task 28c). It holds the number, the sum, the count of lines and the
    /// link to the order in the panel, and nothing that the buyer typed: the name, the email, the phone and the address are
    /// read in the panel, behind the sign-in.
    /// </summary>
    public static EmailMessage NewOrderForAdmin(Order order, string toAddress, string adminUrl)
    {
        var (subject, body) = EmailTexts.Default.Message("adminNewOrder");
        var values = new Dictionary<string, string>
        {
            ["number"] = order.Number.ToString(CultureInfo.InvariantCulture),
            ["total"] = Money(order.TotalAmount, order.Currency),
            ["lineCount"] = order.Items.Count.ToString(CultureInfo.InvariantCulture),
            ["link"] = $"{adminUrl.TrimEnd('/')}/orders/{order.Number.ToString(CultureInfo.InvariantCulture)}",
        };
        return new EmailMessage(toAddress, EmailTexts.Fill(subject, values), EmailTexts.Fill(body, values));
    }

    /// <summary>The address of the page "My order" for this order. The token is the key, so the link is private to the buyer.</summary>
    public static string Link(Order order, string storefrontUrl) =>
        $"{storefrontUrl.TrimEnd('/')}/my-order?token={Uri.EscapeDataString(order.AccessToken ?? string.Empty)}";

    // The smallest unit is a hundredth for the currencies of the store (BGN, EUR). Other currencies are not offered yet.
    public static string Money(long amount, string currency) =>
        $"{(amount / 100m).ToString("0.00", CultureInfo.InvariantCulture)} {currency}";

    private static EmailMessage Build(string key, Order order, string storefrontUrl, Dictionary<string, string> extra)
    {
        var (subject, body) = EmailTexts.Default.Message(key);
        var values = new Dictionary<string, string>(extra)
        {
            ["number"] = order.Number.ToString(CultureInfo.InvariantCulture),
            ["name"] = order.CustomerName,
            ["total"] = Money(order.TotalAmount, order.Currency),
            ["link"] = Link(order, storefrontUrl),
        };
        return new EmailMessage(order.Email, EmailTexts.Fill(subject, values), EmailTexts.Fill(body, values));
    }
}
