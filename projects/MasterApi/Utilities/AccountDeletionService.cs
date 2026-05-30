using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public interface IAccountDeletionService
{
    /// <summary>Finalizes every account whose cooldown has elapsed. Returns the number deleted.</summary>
    Task<int> ProcessDueDeletionsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Finalizes account deletions whose 24h cooldown has elapsed: purges player data on
/// every active game server, emails the player a confirmation just before removal, and
/// then deletes the master account record.
/// </summary>
public sealed class AccountDeletionService(
    MasterDbContext db,
    GameServerAccountPurgeService purgeService,
    IMasterEmailService emailService,
    ILogger<AccountDeletionService> logger) : IAccountDeletionService
{
    public async Task<int> ProcessDueDeletionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await db.PlayerAccounts
            .Where(account => account.DeletionScheduledAtUtc != null && account.DeletionScheduledAtUtc <= now)
            .ToListAsync(cancellationToken);

        var deletedCount = 0;
        foreach (var account in due)
        {
            if (await FinalizeAsync(account, cancellationToken))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private async Task<bool> FinalizeAsync(PlayerAccount account, CancellationToken cancellationToken)
    {
        var email = account.Email;
        var displayName = account.DisplayName;
        var locale = account.PreferredLocale;

        try
        {
            // Remove all game-server data first. If any shard cannot confirm the purge we
            // keep the account so the deletion is retried on the next pass.
            await purgeService.PurgeAsync(email, cancellationToken);
        }
        catch (GameServerPurgeException ex)
        {
            logger.LogWarning(
                ex,
                "Deferring account deletion for {Email} because a game server could not be purged.",
                email);
            return false;
        }

        // Send the confirming email just before the account is actually removed.
        try
        {
            await emailService.SendAccountDeletionCompletedEmailAsync(email, displayName, locale, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send account deletion confirmation email to {Email}.", email);
        }

        db.PlayerAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Finalized deletion of account {Email}.", email);
        return true;
    }
}

public sealed class AccountDeletionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AccountDeletionOptions> options,
    ILogger<AccountDeletionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, options.Value.ProcessingIntervalMinutes);
        var delay = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
                await service.ProcessDueDeletionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Account deletion processing pass failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
