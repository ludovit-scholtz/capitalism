using System.ComponentModel.DataAnnotations;
using Api.Configuration;
using Api.Data;
using Api.Utilities;
using Microsoft.Extensions.Options;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Called by the master server when a player deletes their account. Removes the
    /// player's game data from this shard: destroys every building except banks,
    /// transfers banks to the government (0% deposit / 20% lending), and deletes the
    /// player and their companies. Authenticated by the shared registration key.
    /// </summary>
    public async Task<PurgePlayerAccountFromMasterPayload> PurgePlayerAccountFromMaster(
        PurgePlayerAccountFromMasterInput input,
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

        var playerEmail = input.PlayerEmail?.Trim();
        if (string.IsNullOrWhiteSpace(playerEmail))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player email is required.")
                    .SetCode("PLAYER_EMAIL_REQUIRED")
                    .Build());
        }

        var purgeService = new PlayerAccountPurgeService(db);
        var result = await purgeService.PurgeAsync(playerEmail, ct);

        return new PurgePlayerAccountFromMasterPayload
        {
            PlayerFound = result.PlayerFound,
            CompaniesRemoved = result.CompaniesRemoved,
            BuildingsDestroyed = result.BuildingsDestroyed,
            BanksTransferredToGovernment = result.BanksTransferredToGovernment,
        };
    }
}

public sealed class PurgePlayerAccountFromMasterInput
{
    [Required]
    public string RegistrationKey { get; set; } = string.Empty;

    [Required]
    public string ServerKey { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string PlayerEmail { get; set; } = string.Empty;
}

public sealed class PurgePlayerAccountFromMasterPayload
{
    public bool PlayerFound { get; set; }

    public int CompaniesRemoved { get; set; }

    public int BuildingsDestroyed { get; set; }

    public int BanksTransferredToGovernment { get; set; }
}
