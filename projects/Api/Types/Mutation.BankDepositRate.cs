using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Mutation to schedule a dynamic deposit interest rate change for a bank building.
/// The new rate is applied to all active deposits 24 ticks after the mutation is called.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Schedules a deposit interest rate change for a bank building.
    ///
    /// <para>
    /// Only the owning player may call this mutation.
    /// The new rate must be in the range [0%, 50%].
    /// The rate change becomes effective 24 ticks from the current tick, at which point
    /// the tick processor will update all active deposits at this bank to the new rate.
    /// Any previous pending rate change is replaced by this call.
    /// </para>
    ///
    /// <returns>Updated <see cref="BankInfoSummary"/> including the pending-rate fields.</returns>
    /// </summary>
    [Authorize]
    public async Task<BankInfoSummary> UpdateBankDepositRate(
        UpdateBankDepositRateInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Validate new rate bounds (0%–50%)
        if (input.NewRatePercent < 0m || input.NewRatePercent > 50m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Deposit interest rate must be between 0% and 50%.")
                    .SetCode("INVALID_INTEREST_RATE")
                    .Build());
        }

        var bank = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.BankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null || bank.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found or you do not own it.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        if (!bank.BaseCapitalDeposited)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank must have completed its base capital deposit before rates can be changed.")
                    .SetCode("BANK_NOT_ACTIVATED")
                    .Build());
        }

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync();
        if (gameState is null)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Game state not found.").SetCode("GAME_STATE_MISSING").Build());

        var currentTick = gameState.CurrentTick;
        const int effectiveDelayTicks = 24;
        var effectiveTick = currentTick + effectiveDelayTicks;

        // Effective UTC is estimated from the current time plus 24-tick duration in real seconds.
        var effectiveUtc = DateTime.UtcNow.AddSeconds(effectiveDelayTicks * gameState.TickIntervalSeconds);

        // Replace any existing pending rate change
        var existingPending = await db.BankDepositRateHistories
            .FirstOrDefaultAsync(h => h.BankBuildingId == bank.Id && !h.IsApplied);
        if (existingPending is not null)
            db.BankDepositRateHistories.Remove(existingPending);

        // Create an audit record for this rate change
        var historyEntry = new BankDepositRateHistory
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            PreviousRatePercent = bank.DepositInterestRatePercent ?? 0m,
            NewRatePercent = input.NewRatePercent,
            EffectiveTick = effectiveTick,
            EffectiveUtc = effectiveUtc,
            ScheduledAtTick = currentTick,
            ScheduledAtUtc = DateTime.UtcNow,
            AffectedDepositCount = 0, // filled in by BankInterestPhase when applied
            ChangedByPlayerId = userId,
            IsApplied = false,
        };

        db.BankDepositRateHistories.Add(historyEntry);

        // Store the pending rate on the building so it can be displayed in the UI
        bank.PendingDepositInterestRatePercent = input.NewRatePercent;
        bank.PendingDepositRateEffectiveTick = effectiveTick;

        await db.SaveChangesAsync();

        return await BuildBankInfoAsync(db, bank);
    }
}
