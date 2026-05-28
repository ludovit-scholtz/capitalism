using System.Net.Mail;
using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Utilities;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    [HotChocolate.Authorization.Authorize]
    public async Task<bool> SendAdminTestEmail(
        SendAdminTestEmailInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var actorEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, actorEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Test email sending requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var recipientEmail = NormalizeEmailAddress(input.RecipientEmail);
        var displayName = NormalizeDisplayName(input.RecipientDisplayName, recipientEmail);
        var message = NormalizeTestEmailMessage(input.Message);

        return await emailService.SendAdminTestEmailAsync(
            recipientEmail,
            displayName,
            input.Locale,
            message,
            actorEmail,
            cancellationToken);
    }

    private static string NormalizeEmailAddress(string email)
    {
        var normalized = email.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 320)
        {
            throw BuildInvalidTestEmailInput("Recipient email is required.");
        }

        try
        {
            var parsed = new MailAddress(normalized);
            if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildInvalidTestEmailInput("Recipient email must be a valid email address.");
            }

            return parsed.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw BuildInvalidTestEmailInput("Recipient email must be a valid email address.");
        }
    }

    private static string NormalizeDisplayName(string? displayName, string fallback)
    {
        var normalized = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized.Length > 120 ? normalized[..120] : normalized;
    }

    private static string NormalizeTestEmailMessage(string? message)
    {
        var normalized = message?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Email delivery test.";
        }

        return normalized.Length > 2000 ? normalized[..2000] : normalized;
    }

    private static GraphQLException BuildInvalidTestEmailInput(string message)
    {
        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(message)
                .SetCode("INVALID_TEST_EMAIL_INPUT")
                .Build());
    }
}

public sealed class SendAdminTestEmailInput
{
    public string RecipientEmail { get; set; } = string.Empty;

    public string? RecipientDisplayName { get; set; }

    public string Locale { get; set; } = "en";

    public string? Message { get; set; }
}
