using System.ComponentModel.DataAnnotations;
using Api.Configuration;
using Api.Data;
using Capitalism.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Types;

public sealed partial class Mutation
{
    public async Task<SyncPersonalAccountNameFromMasterPayload> SyncPersonalAccountNameFromMaster(
        SyncPersonalAccountNameFromMasterInput input,
        [Service] AppDbContext db,
        [Service] IOptions<MasterServerRegistrationOptions> options,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        var expectedRegistrationKey = options.Value.RegistrationKey.Trim();
        if (string.IsNullOrWhiteSpace(expectedRegistrationKey)
            || !string.Equals(expectedRegistrationKey, input.RegistrationKey?.Trim(), StringComparison.Ordinal))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid game server registration key.")
                    .SetCode("INVALID_REGISTRATION_KEY")
                    .Build());
        }

        var serverKey = input.ServerKey?.Trim();
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Server key is required.")
                    .SetCode("SERVER_KEY_REQUIRED")
                    .Build());
        }

        var expectedServerKey = options.Value.ServerKey.Trim();
        if (string.IsNullOrWhiteSpace(expectedServerKey)
            || !string.Equals(expectedServerKey, serverKey, StringComparison.Ordinal))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid server key.")
                    .SetCode("INVALID_SERVER_KEY")
                    .Build());
        }

        var playerEmail = input.PlayerEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(playerEmail))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player email is required.")
                    .SetCode("PLAYER_EMAIL_REQUIRED")
                    .Build());
        }

        var personalAccountName = input.PersonalAccountName?.Trim();
        if (string.IsNullOrWhiteSpace(personalAccountName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name is required.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_REQUIRED")
                    .Build());
        }

        if (personalAccountName.Length > 40)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name must be 40 characters or fewer.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_TOO_LONG")
                    .Build());
        }

        string? normalizedGender = null;
        if (input.Gender is not null)
        {
            var trimmedGender = input.Gender.Trim().ToUpperInvariant();
            if (!PlayerGender.IsValid(trimmedGender))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Gender must be MALE, FEMALE, or UNSPECIFIED.")
                        .SetCode("INVALID_GENDER")
                        .Build());
            }

            normalizedGender = trimmedGender;
        }

        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == playerEmail, ct);
        if (player is null)
        {
            return new SyncPersonalAccountNameFromMasterPayload
            {
                PlayerEmail = playerEmail,
                PersonalAccountName = personalAccountName,
                PlayerFound = false,
                WasUpdated = false,
            };
        }

        var wasUpdated = !string.Equals(player.DisplayName, personalAccountName, StringComparison.Ordinal)
            || (normalizedGender is not null && !string.Equals(player.Gender, normalizedGender, StringComparison.Ordinal));
        if (wasUpdated)
        {
            player.DisplayName = personalAccountName;
            if (normalizedGender is not null)
            {
                player.Gender = normalizedGender;
            }
            await db.SaveChangesAsync(ct);
        }

        return new SyncPersonalAccountNameFromMasterPayload
        {
            PlayerId = player.Id,
            PlayerEmail = player.Email,
            PersonalAccountName = player.DisplayName,
            Gender = player.Gender,
            PlayerFound = true,
            WasUpdated = wasUpdated,
        };
    }
}

public sealed class SyncPersonalAccountNameFromMasterInput
{
    [Required]
    public string RegistrationKey { get; set; } = string.Empty;

    [Required]
    public string ServerKey { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string PlayerEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string PersonalAccountName { get; set; } = string.Empty;

    public string? Gender { get; set; }
}

public sealed class SyncPersonalAccountNameFromMasterPayload
{
    public Guid? PlayerId { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public string PersonalAccountName { get; set; } = string.Empty;

    public string Gender { get; set; } = PlayerGender.Unspecified;

    public bool PlayerFound { get; set; }

    public bool WasUpdated { get; set; }
}
