using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Infrastructure.Email;

/// <summary>
/// Sends email over SMTP with MailKit (decision 0031). Without a configured host the message is skipped, so a demo without keys
/// keeps working. The recipient, the body, the server and the password never reach the log or an exception text.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogWarning("Email is not configured, message skipped. Message: {MessageId}", message.Id);
            return;
        }

        if (settings.MissingSettings() is { Count: > 0 } missing)
        {
            throw new InvalidOperationException($"Email is not configured. Missing: {string.Join(", ", missing)}.");
        }

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                settings.UseTls ? SecureSocketOptions.Auto : SecureSocketOptions.None,
                cancellationToken);
            if (!string.IsNullOrEmpty(settings.User))
            {
                await client.AuthenticateAsync(settings.User, settings.Password, cancellationToken);
            }

            await client.SendAsync(Build(message, settings), cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Any failure is reported by its kind only: the texts of the library and the network can name the server,
            // the user or the recipient.
            var failure = exception.GetType().Name;
            logger.LogWarning("An email could not be sent. Message: {MessageId}. Failure: {Failure}", message.Id, failure);
            throw new EmailDeliveryException($"The email could not be sent. Failure: {failure}.");
        }
    }

    private static MimeMessage Build(EmailMessage message, EmailOptions settings)
    {
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();
        return mime;
    }
}
