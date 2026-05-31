using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities.Discord;

/// <summary>
/// Pure, transport-agnostic logic behind the Discord bot slash commands. It operates directly on
/// the master database and shared gold/ranking helpers so it can be unit tested without a live
/// Discord connection. <see cref="DiscordBotHostedService"/> wires the Discord.Net interactions to
/// these methods.
/// </summary>
public sealed class DiscordCommandService
{
    private readonly MasterDbContext _db;
    private readonly IOptions<DiscordBotOptions> _botOptions;
    private readonly IOptions<GoldTokenTransferOptions> _transferOptions;
    private readonly MasterRankingService _rankingService;
    private readonly ILogger<DiscordCommandService> _logger;

    public DiscordCommandService(
        MasterDbContext db,
        IOptions<DiscordBotOptions> botOptions,
        IOptions<GoldTokenTransferOptions> transferOptions,
        MasterRankingService rankingService,
        ILogger<DiscordCommandService> logger)
    {
        _db = db;
        _botOptions = botOptions;
        _transferOptions = transferOptions;
        _rankingService = rankingService;
        _logger = logger;
    }

    /// <summary>
    /// Links a Discord user to a master account using a one-time code the player generated in the
    /// master frontend, then records the Discord-player bounty event.
    /// </summary>
    public async Task<DiscordCommandResult> VerifyAsync(
        string discordUserId,
        string? discordUsername,
        string? code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(discordUserId))
        {
            return DiscordCommandResult.Fail("Could not resolve your Discord user id.");
        }

        var normalizedCode = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return DiscordCommandResult.Fail(
                "Please provide your verification code. Generate one in the **Account** page of the master website, then run the command again.");
        }

        var now = DateTime.UtcNow;
        var account = await _db.PlayerAccounts.FirstOrDefaultAsync(
            candidate => candidate.DiscordLinkCode == normalizedCode,
            cancellationToken);

        if (account is null
            || account.DiscordLinkCodeExpiresAtUtc is null
            || account.DiscordLinkCodeExpiresAtUtc <= now)
        {
            return DiscordCommandResult.Fail(
                "That verification code is invalid or has expired. Generate a fresh code on the master website and try again.");
        }

        // Detach the code from any other account that might still reference this Discord user.
        var previouslyLinked = await _db.PlayerAccounts
            .Where(candidate => candidate.DiscordUserId == discordUserId && candidate.Id != account.Id)
            .ToListAsync(cancellationToken);
        foreach (var stale in previouslyLinked)
        {
            stale.DiscordUserId = null;
            stale.DiscordUsername = null;
        }

        account.DiscordUserId = discordUserId;
        account.DiscordUsername = string.IsNullOrWhiteSpace(discordUsername) ? null : discordUsername.Trim();
        account.DiscordLinkCode = null;
        account.DiscordLinkCodeExpiresAtUtc = null;

        await _db.SaveChangesAsync(cancellationToken);
        await TryAwardDiscordBountyAsync(account.Email, discordUserId, account.DiscordUsername, cancellationToken);

        return DiscordCommandResult.Ok(
            $"✅ Your Discord account is now linked to **{account.Email}**. The Discord bounty has been recorded — keep playing to climb the global ranking!");
    }

    /// <summary>Creates a tokenized-gold deposit request and returns the address and required note.</summary>
    public async Task<DiscordCommandResult> CreateDepositAsync(
        string discordUserId,
        string? network,
        string? senderAddress,
        CancellationToken cancellationToken = default)
    {
        var account = await ResolveLinkedAccountAsync(discordUserId, cancellationToken);
        if (account is null)
        {
            return NotLinkedResult();
        }

        string normalizedNetwork;
        try
        {
            normalizedNetwork = GoldTokenRequests.NormalizeNetwork(network ?? _botOptions.Value.DefaultNetwork);
        }
        catch (GraphQLException ex)
        {
            return DiscordCommandResult.Fail(ex.Errors.FirstOrDefault()?.Message ?? "Invalid network.");
        }

        var request = GoldTokenRequests.CreateDepositRequest(
            _db, account, normalizedNetwork, senderAddress, 0m, _transferOptions.Value);
        await _db.SaveChangesAsync(cancellationToken);

        var message = new StringBuilder();
        message.AppendLine($"💰 **Gold deposit request created** ({normalizedNetwork})");
        message.AppendLine($"Send tokenized gold (asset id `{request.AssetId}`) to:");
        message.AppendLine($"`{request.DepositAddress}`");
        message.AppendLine("You **must** put this exact note in the transaction note field:");
        message.AppendLine($"`{request.NoteText}`");
        message.AppendLine("Your balance is credited automatically once the transaction is confirmed.");
        return DiscordCommandResult.Ok(message.ToString().TrimEnd());
    }

    /// <summary>Creates a tokenized-gold withdrawal request, debiting the player's balance.</summary>
    public async Task<DiscordCommandResult> CreateWithdrawalAsync(
        string discordUserId,
        decimal amount,
        string? destinationAddress,
        string? network,
        CancellationToken cancellationToken = default)
    {
        var account = await ResolveLinkedAccountAsync(discordUserId, cancellationToken);
        if (account is null)
        {
            return NotLinkedResult();
        }

        try
        {
            var request = GoldTokenRequests.CreateWithdrawalRequest(
                _db,
                account,
                network ?? _botOptions.Value.DefaultNetwork,
                amount,
                destinationAddress ?? string.Empty);
            await _db.SaveChangesAsync(cancellationToken);

            return DiscordCommandResult.Ok(
                $"🏧 **Withdrawal request created** for {request.Amount:0.########} g of tokenized gold ({request.Network}) to `{request.DestinationAddress}`. "
                + "An administrator will process it shortly. Your in-game gold balance has been reserved.");
        }
        catch (GraphQLException ex)
        {
            return DiscordCommandResult.Fail(ex.Errors.FirstOrDefault()?.Message ?? "Could not create withdrawal request.");
        }
    }

    /// <summary>Builds the help message for the help command.</summary>
    public string BuildHelp()
    {
        var prefix = _botOptions.Value.NormalizedCommandPrefix();
        var options = _botOptions.Value;
        var message = new StringBuilder();
        message.AppendLine("🏦 **Capitalism — multiplayer economic strategy**");
        message.AppendLine($"Play now: {options.MasterFrontendUrl}");
        message.AppendLine($"Community: {options.DiscordInviteUrl}");
        message.AppendLine();
        message.AppendLine("**How to play (the basics)**");
        message.AppendLine("• Register on the website, pick a game server and found your first company.");
        message.AppendLine("• Buy land, construct buildings, manufacture goods and sell them on the market.");
        message.AppendLine("• Take loans, trade on the stock and FX markets, and grow your industrial empire.");
        message.AppendLine("• Compete for the top spot on the global ranking and earn bounties.");
        message.AppendLine();
        message.AppendLine("**Bot commands**");
        message.AppendLine($"• `/{prefix}-verify <code>` — link your Discord to your account (generate the code on the website) and claim the Discord bounty.");
        message.AppendLine($"• `/{prefix}-deposit [network] [sender]` — get a tokenized-gold deposit address and note.");
        message.AppendLine($"• `/{prefix}-withdraw <amount> <address> [network]` — request a tokenized-gold withdrawal.");
        message.AppendLine($"• `/{prefix}-help` — show this message.");
        return message.ToString().TrimEnd();
    }

    private async Task<PlayerAccount?> ResolveLinkedAccountAsync(string discordUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(discordUserId))
        {
            return null;
        }

        return await _db.PlayerAccounts.FirstOrDefaultAsync(
            candidate => candidate.DiscordUserId == discordUserId,
            cancellationToken);
    }

    private DiscordCommandResult NotLinkedResult()
    {
        var prefix = _botOptions.Value.NormalizedCommandPrefix();
        return DiscordCommandResult.Fail(
            $"Your Discord account is not linked yet. Generate a code on {_botOptions.Value.MasterFrontendUrl} and run `/{prefix}-verify <code>` first.");
    }

    private async Task TryAwardDiscordBountyAsync(
        string email,
        string discordUserId,
        string? discordUsername,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                discordUserId,
                discordHandle = discordUsername,
            });

            await _rankingService.IngestEventAsync(
                MasterRankingBountyCodes.DiscordPlayer,
                email,
                serverKey: "discord-bot",
                externalEventId: null,
                uniqueScopeKey: null,
                idempotencyKey: null,
                proofReference: $"discord:{discordUserId}",
                payloadJson: payload,
                occurredAtUtc: DateTime.UtcNow,
                telemetryValidation: null,
                cancellationToken: cancellationToken);
        }
        catch (GraphQLException ex)
        {
            // Duplicate proof / cooldown simply means the bounty was already recorded — not fatal.
            _logger.LogInformation(
                ex,
                "Discord bounty not recorded for {Email} (likely already claimed).",
                email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record Discord bounty for {Email}.", email);
        }
    }
}

/// <summary>Result of a Discord command: a user-facing message and whether it succeeded.</summary>
public sealed record DiscordCommandResult(bool Success, string Message)
{
    public static DiscordCommandResult Ok(string message) => new(true, message);

    public static DiscordCommandResult Fail(string message) => new(false, message);
}
