using System.Net;
using System.Net.Mail;
using MasterApi.Configuration;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetEmailAsync(string recipientEmail, string recipientDisplayName, string resetLink, CancellationToken cancellationToken);
}

public sealed class PasswordResetEmailSender(
    IOptions<AuthOptions> authOptions,
    ILogger<PasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string resetLink,
        CancellationToken cancellationToken)
    {
        var options = authOptions.Value;
        if (string.IsNullOrWhiteSpace(options.PasswordResetSmtpHost))
        {
            logger.LogInformation(
                "Skipping password reset email send because SMTP host is not configured.");
            return;
        }

        using var message = new MailMessage
        {
            Subject = "Capitalism password reset",
            Body = $"""
                   Hello {recipientDisplayName},

                   We received a request to reset your Capitalism password.
                   Open this link to continue (expires in {options.PasswordResetTokenLifetimeMinutes} minutes):
                   {resetLink}

                   If you did not request this reset, you can safely ignore this email.
                   """,
            IsBodyHtml = false,
        };
        message.From = new MailAddress(options.PasswordResetEmailFrom, options.PasswordResetEmailFromName);
        message.To.Add(new MailAddress(recipientEmail));

        using var smtpClient = new SmtpClient(options.PasswordResetSmtpHost, options.PasswordResetSmtpPort)
        {
            EnableSsl = options.PasswordResetSmtpEnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(options.PasswordResetSmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(
                options.PasswordResetSmtpUsername,
                options.PasswordResetSmtpPassword);
        }

        using var registration = cancellationToken.Register(static state => ((SmtpClient)state!).SendAsyncCancel(), smtpClient);
        await smtpClient.SendMailAsync(message, cancellationToken);
    }
}
