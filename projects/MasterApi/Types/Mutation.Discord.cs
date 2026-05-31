using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Utilities.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Issues a fresh single-use code the authenticated player types into the Discord verify
    /// command (<c>/cap5-verify</c> in production, <c>/cap5stage-verify</c> in staging) to link
    /// their Discord account and claim the Discord bounty. Any previously issued code is replaced.
    /// </summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<DiscordLinkCodeInfo> RequestDiscordLinkCode(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<DiscordBotOptions> discordOptions)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var lifetimeMinutes = Math.Max(1, discordOptions.Value.LinkCodeLifetimeMinutes);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(lifetimeMinutes);

        player.DiscordLinkCode = DiscordLinkCode.Generate();
        player.DiscordLinkCodeExpiresAtUtc = expiresAtUtc;
        await db.SaveChangesAsync();

        return new DiscordLinkCodeInfo
        {
            Code = player.DiscordLinkCode,
            ExpiresAtUtc = expiresAtUtc,
            CommandPrefix = discordOptions.Value.NormalizedCommandPrefix(),
            DiscordInviteUrl = discordOptions.Value.DiscordInviteUrl,
        };
    }

    /// <summary>
    /// Called by a game server to forward an in-game chat message to the bridged Discord channel.
    /// Authenticated by the shared master registration key plus a registered server key. The
    /// message is queued for the Discord bot hosted service to deliver.
    /// </summary>
    public async Task<ForwardChatToDiscordPayload> ForwardInGameChatToDiscord(
        ForwardChatToDiscordInput input,
        [Service] MasterDbContext db,
        [Service] DiscordChatRelay relay,
        [Service] IOptions<MasterServerOptions> masterServerOptions)
    {
        var expectedRegistrationKey = masterServerOptions.Value.RegistrationKey?.Trim();
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

        var server = await db.GameServers
            .AsNoTracking()
            .FirstOrDefaultAsync(node => node.ServerKey == serverKey);
        if (server is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Unknown game server key.")
                    .SetCode("INVALID_SERVER_KEY")
                    .Build());
        }

        var content = input.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ForwardChatToDiscordPayload { Accepted = false };
        }

        var author = string.IsNullOrWhiteSpace(input.AuthorDisplayName) ? "Player" : input.AuthorDisplayName.Trim();
        relay.Enqueue(new DiscordChatRelayMessage(
            author,
            content.Length > 500 ? content[..500] : content,
            string.IsNullOrWhiteSpace(server.DisplayName) ? serverKey : server.DisplayName));

        return new ForwardChatToDiscordPayload { Accepted = true };
    }
}

public sealed class DiscordLinkCodeInfo
{
    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Command prefix to use (for example <c>cap5</c> or <c>cap5stage</c>).</summary>
    public string CommandPrefix { get; set; } = "cap5";

    public string DiscordInviteUrl { get; set; } = string.Empty;
}

public sealed class ForwardChatToDiscordInput
{
    public string RegistrationKey { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class ForwardChatToDiscordPayload
{
    public bool Accepted { get; set; }
}
