using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Utilities;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Marks the authenticated player's own account for deletion after a cooldown
    /// period. The deletion is confirmed by re-entering the account email and can be
    /// cancelled until the cooldown elapses.
    /// </summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<AccountDeletionStatusPayload> RequestAccountDeletion(
        RequestAccountDeletionInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<AccountDeletionOptions> deletionOptions,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw BuildAccountDeletionError("Player not found.", "PLAYER_NOT_FOUND");

        var confirmationEmail = input.ConfirmationEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(confirmationEmail))
        {
            throw BuildAccountDeletionError(
                "Confirmation email is required to delete your account.",
                "CONFIRMATION_EMAIL_REQUIRED");
        }

        if (!string.Equals(confirmationEmail, player.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw BuildAccountDeletionError(
                "The confirmation email does not match your account email.",
                "CONFIRMATION_EMAIL_MISMATCH");
        }

        if (player.DeletionRequestedAtUtc is not null)
        {
            // Already pending: keep the original schedule and return current status.
            return ToDeletionStatus(player.DeletionRequestedAtUtc, player.DeletionScheduledAtUtc);
        }

        var cooldownHours = Math.Max(0, deletionOptions.Value.CooldownHours);
        var now = DateTime.UtcNow;
        var scheduledAtUtc = now.AddHours(cooldownHours);
        player.DeletionRequestedAtUtc = now;
        player.DeletionScheduledAtUtc = scheduledAtUtc;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendAccountDeletionRequestedEmailAsync(player, scheduledAtUtc, cancellationToken);
        }
        catch (Exception)
        {
            // Email delivery must not block the deletion request itself.
        }

        return ToDeletionStatus(player.DeletionRequestedAtUtc, player.DeletionScheduledAtUtc);
    }

    /// <summary>Cancels a pending deletion of the authenticated player's own account.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<AccountDeletionStatusPayload> CancelAccountDeletion(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        CancellationToken cancellationToken)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw BuildAccountDeletionError("Player not found.", "PLAYER_NOT_FOUND");

        if (player.DeletionRequestedAtUtc is null)
        {
            return ToDeletionStatus(null, null);
        }

        player.DeletionRequestedAtUtc = null;
        player.DeletionScheduledAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        return ToDeletionStatus(null, null);
    }

    private static AccountDeletionStatusPayload ToDeletionStatus(
        DateTime? requestedAtUtc,
        DateTime? scheduledAtUtc)
    {
        return new AccountDeletionStatusPayload
        {
            IsPendingDeletion = requestedAtUtc is not null,
            DeletionRequestedAtUtc = requestedAtUtc,
            DeletionScheduledAtUtc = scheduledAtUtc,
        };
    }

    private static GraphQLException BuildAccountDeletionError(string message, string code)
    {
        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(message)
                .SetCode(code)
                .Build());
    }
}

public sealed class RequestAccountDeletionInput
{
    public string ConfirmationEmail { get; set; } = string.Empty;
}

public sealed class AccountDeletionStatusPayload
{
    public bool IsPendingDeletion { get; set; }

    public DateTime? DeletionRequestedAtUtc { get; set; }

    public DateTime? DeletionScheduledAtUtc { get; set; }
}
