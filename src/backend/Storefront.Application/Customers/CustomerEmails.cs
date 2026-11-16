using System.Globalization;
using Storefront.Application.Contracts;
using Storefront.Application.Emails;

namespace Storefront.Application.Customers;

/// <summary>
/// Builds the emails of a buyer's account (decision 0039). Plain text; the wording is in <c>Emails/Texts/bg.json</c>. The links
/// carry the address and the token of Identity in the query, which the page of the shop sends back in a body.
/// </summary>
public static class CustomerEmails
{
    public static EmailMessage Confirmation(string to, string name, string storefrontUrl, string token, int lifetimeMinutes) =>
        Build("confirmEmail", to, name, new Dictionary<string, string>
        {
            ["link"] = Link(storefrontUrl, "/account/confirm-email", to, token),
            ["minutes"] = lifetimeMinutes.ToString(CultureInfo.InvariantCulture),
        });

    public static EmailMessage PasswordReset(string to, string name, string storefrontUrl, string token, int lifetimeMinutes) =>
        Build("passwordReset", to, name, new Dictionary<string, string>
        {
            ["link"] = Link(storefrontUrl, "/account/reset-password", to, token),
            ["minutes"] = lifetimeMinutes.ToString(CultureInfo.InvariantCulture),
        });

    /// <summary>
    /// For someone who tried to register an address that already has an account. It goes to the owner of the address, so the
    /// registration page can answer the same for a new and an old address.
    /// </summary>
    public static EmailMessage AlreadyRegistered(string to, string name, string storefrontUrl) =>
        Build("alreadyRegistered", to, name, new Dictionary<string, string>
        {
            ["signIn"] = $"{storefrontUrl.TrimEnd('/')}/account/login",
            ["reset"] = $"{storefrontUrl.TrimEnd('/')}/account/forgot-password",
        });

    private static string Link(string storefrontUrl, string path, string email, string token) =>
        $"{storefrontUrl.TrimEnd('/')}{path}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    private static EmailMessage Build(string key, string to, string name, Dictionary<string, string> extra)
    {
        var (subject, body) = EmailTexts.Default.Message(key);
        var values = new Dictionary<string, string>(extra) { ["name"] = name };
        return new EmailMessage(to, EmailTexts.Fill(subject, values), EmailTexts.Fill(body, values));
    }
}
