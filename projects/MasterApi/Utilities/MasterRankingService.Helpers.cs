using System.Text.Json;
using Capitalism.Shared.Security;
using HotChocolate;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MasterApi.Utilities;

public sealed partial class MasterRankingService
{
    private static string BuildUniquenessKey(MasterRankingBountyDefinition definition, MasterRankingEvent rankingEvent)
    {
        var dateKey = rankingEvent.OccurredAtUtc.Date.ToString("yyyyMMdd");
        var playerKey = rankingEvent.PlayerAccountId?.ToString() ?? rankingEvent.PlayerEmail;
        return definition.CooldownMode switch
        {
            RankingCooldownMode.None => $"{definition.Code}:{rankingEvent.Id}",
            RankingCooldownMode.UtcDay => $"{definition.Code}:{playerKey}:{dateKey}",
            RankingCooldownMode.UtcDayPerServer => $"{definition.Code}:{playerKey}:{rankingEvent.ServerKey ?? "global"}:{dateKey}",
            RankingCooldownMode.Once => $"{definition.Code}:{playerKey}:once",
            RankingCooldownMode.PerUniqueKey => $"{definition.Code}:{playerKey}:{rankingEvent.UniqueScopeKey ?? rankingEvent.ExternalEventId ?? rankingEvent.Id.ToString()}",
            _ => $"{definition.Code}:{playerKey}:{dateKey}:{rankingEvent.Id}",
        };
    }

    private async Task<Guid?> EnsureTelemetryPlayerAccountAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var existingPlayerId = await db.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.Email == normalizedEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPlayerId.HasValue)
        {
            return existingPlayerId.Value;
        }

        await using var provisionLock = await PlayerAccountProvisionLocks.AcquireAsync(normalizedEmail, cancellationToken);

        existingPlayerId = await db.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.Email == normalizedEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPlayerId.HasValue)
        {
            return existingPlayerId.Value;
        }

        var now = DateTime.UtcNow;
        var fallbackDisplayName = PlayerDisplayNameProvisioning.ResolveDisplayName(
            claimedDisplayName: null,
            normalizedEmail: normalizedEmail,
            subjectClaim: null);

        var shadowAccount = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = fallbackDisplayName,
            PasswordHash = BuildShadowPasswordHash(normalizedEmail),
            CreatedAtUtc = now,
        };

        db.PlayerAccounts.Add(shadowAccount);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return shadowAccount.Id;
        }
        catch (DbUpdateException ex) when (IsPlayerEmailUniqueConstraintViolation(ex))
        {
            db.Entry(shadowAccount).State = EntityState.Detached;
        }

        return await db.PlayerAccounts
            .Where(player => player.Email == normalizedEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildEvaluatorNotes(MasterRankingEvaluationRun run)
    {
        if (run.RewardRecordsCreated > 5000)
        {
            return "Abnormal reward spike detected: created more than 5000 reward records in a single run.";
        }

        return "Hourly ranking evaluation completed successfully.";
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static GraphQLException BuildProofReferenceConflictError()
    {
        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Proof reference already used.")
                .SetCode("PROOF_REFERENCE_CONFLICT")
                .SetExtension("httpStatus", 409)
                .Build());
    }

    private static bool IsProofReferenceUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException postgres)
        {
            return false;
        }

        return postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgres.ConstraintName, "IX_MasterRankingEvents_ProofReference", StringComparison.Ordinal);
    }

    private static bool IsPlayerEmailUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException postgres)
        {
            return false;
        }

        return postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgres.ConstraintName, "IX_PlayerAccounts_Email", StringComparison.Ordinal);
    }
}
