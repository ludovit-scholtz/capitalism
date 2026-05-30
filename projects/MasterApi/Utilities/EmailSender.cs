using Azure;
using Azure.Communication.Email;
using MasterApi.Configuration;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public sealed record EmailAttachmentContent(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record EmailMessageRequest(
    string RecipientEmail,
    string RecipientDisplayName,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    IReadOnlyList<EmailAttachmentContent>? Attachments = null);

public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessageRequest request, CancellationToken cancellationToken);
}

public sealed class AzureCommunicationEmailSender(
    IOptions<EmailOptions> emailOptions,
    ILogger<AzureCommunicationEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessageRequest request, CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.AzureCommunicationServicesConnectionString))
        {
            logger.LogInformation("Skipping email send because Azure Communication Services email is not configured.");
            return false;
        }

        var client = new EmailClient(options.AzureCommunicationServicesConnectionString);
        var content = new EmailContent(request.Subject)
        {
            Html = request.HtmlBody,
            PlainText = request.PlainTextBody,
        };
        var recipients = new EmailRecipients([
            new EmailAddress(request.RecipientEmail, request.RecipientDisplayName),
        ]);
        var message = new Azure.Communication.Email.EmailMessage(
            options.SenderAddress,
            recipients,
            content);

        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attachment in request.Attachments)
            {
                message.Attachments.Add(new EmailAttachment(
                    attachment.FileName,
                    attachment.ContentType,
                    BinaryData.FromBytes(attachment.Content)));
            }
        }

        try
        {
            await client.SendAsync(WaitUntil.Completed, message, cancellationToken);
            return true;
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Azure Communication Services email send failed. Status={Status} ErrorCode={ErrorCode}",
                exception.Status,
                exception.ErrorCode);
            return false;
        }
    }
}
