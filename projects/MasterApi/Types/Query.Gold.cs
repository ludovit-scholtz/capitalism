using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.CostAnalysis.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{

    /// <summary>Returns the authenticated player's own gold token account details (balance and recent transactions).</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<PlayerGoldAccountInfo> GetMyGoldAccount(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var recentTx = await db.GoldTokenTransactions
            .AsNoTracking()
            .Where(tx => tx.PlayerAccountId == player.Id)
            .OrderByDescending(tx => tx.CreatedAtUtc)
            .Take(10)
            .Select(tx => new PlayerGoldTransactionInfo
            {
                Id = tx.Id,
                Amount = tx.Amount,
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                Note = tx.Note,
                CreatedAtUtc = tx.CreatedAtUtc,
            })
            .ToListAsync();

        return new PlayerGoldAccountInfo
        {
            GoldTokenBalance = player.GoldTokenBalance,
            LastUpdatedAtUtc = recentTx.Count > 0 ? recentTx[0].CreatedAtUtc : null,
            RecentTransactions = recentTx,
        };
    }

    /// <summary>Returns all player accounts with their gold token balances. Requires global admin or root admin.</summary>
    [HotChocolate.Authorization.Authorize]
    [Cost(20)]
    public async Task<List<GoldTokenBalanceInfo>> GetGoldTokenBalances(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        return await db.PlayerAccounts
            .AsNoTracking()
            .OrderBy(p => p.Email)
            .Select(p => new GoldTokenBalanceInfo
            {
                PlayerId = p.Id,
                Email = p.Email,
                DisplayName = p.DisplayName,
                GoldTokenBalance = p.GoldTokenBalance,
            })
            .ToListAsync();
    }

    /// <summary>Returns recent gold token transactions (audit log). Requires global admin or root admin.</summary>
    [HotChocolate.Authorization.Authorize]
    [Cost(35)]
    public async Task<List<GoldTokenTransactionInfo>> GetGoldTokenTransactions(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        string? targetEmail = null,
        int limit = 50)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var clampedLimit = Math.Clamp(limit, 1, 200);
        var query = db.GoldTokenTransactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(targetEmail))
        {
            var normalized = targetEmail.Trim().ToLowerInvariant();
            query = query.Where(tx => tx.PlayerEmail == normalized);
        }

        return await query
            .OrderByDescending(tx => tx.CreatedAtUtc)
            .Take(clampedLimit)
            .Select(tx => new GoldTokenTransactionInfo
            {
                Id = tx.Id,
                PlayerEmail = tx.PlayerEmail,
                Amount = tx.Amount,
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                AdminEmail = tx.AdminEmail,
                Note = tx.Note,
                CreatedAtUtc = tx.CreatedAtUtc,
            })
            .ToListAsync();
    }

    [HotChocolate.Authorization.Authorize]
    [Cost(20)]
    public async Task<List<GoldTokenDepositRequestInfo>> GetMyGoldDepositRequests(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        return await db.GoldTokenDepositRequests
            .AsNoTracking()
            .Where(request => request.PlayerAccountId == player.Id)
            .OrderByDescending(request => request.RequestedAtUtc)
            .Select(request => new GoldTokenDepositRequestInfo
            {
                Id = request.Id,
                PlayerAccountId = request.PlayerAccountId,
                PlayerEmail = request.PlayerEmail,
                Network = request.Network,
                AssetId = request.AssetId,
                DepositAddress = request.DepositAddress,
                SenderAddress = request.SenderAddress,
                Amount = request.Amount,
                Status = request.Status,
                RequestedAtUtc = request.RequestedAtUtc,
                ProcessedAtUtc = request.ProcessedAtUtc,
                ProcessedByEmail = request.ProcessedByEmail,
                AdminNote = request.AdminNote,
            })
            .ToListAsync();
    }

    [HotChocolate.Authorization.Authorize]
    [Cost(20)]
    public async Task<List<GoldTokenWithdrawalRequestInfo>> GetMyGoldWithdrawalRequests(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        return await db.GoldTokenWithdrawalRequests
            .AsNoTracking()
            .Where(request => request.PlayerAccountId == player.Id)
            .OrderByDescending(request => request.RequestedAtUtc)
            .Select(request => new GoldTokenWithdrawalRequestInfo
            {
                Id = request.Id,
                PlayerAccountId = request.PlayerAccountId,
                PlayerEmail = request.PlayerEmail,
                Network = request.Network,
                AssetId = request.AssetId,
                DestinationAddress = request.DestinationAddress,
                Amount = request.Amount,
                Status = request.Status,
                RequestedAtUtc = request.RequestedAtUtc,
                ProcessedAtUtc = request.ProcessedAtUtc,
                ProcessedByEmail = request.ProcessedByEmail,
                AdminNote = request.AdminNote,
            })
            .ToListAsync();
    }

    [HotChocolate.Authorization.Authorize]
    [Cost(25)]
    public async Task<List<GoldTokenDepositRequestInfo>> GetGoldTokenDepositRequests(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        return await db.GoldTokenDepositRequests
            .AsNoTracking()
            .OrderByDescending(request => request.RequestedAtUtc)
            .Select(request => new GoldTokenDepositRequestInfo
            {
                Id = request.Id,
                PlayerAccountId = request.PlayerAccountId,
                PlayerEmail = request.PlayerEmail,
                Network = request.Network,
                AssetId = request.AssetId,
                DepositAddress = request.DepositAddress,
                SenderAddress = request.SenderAddress,
                Amount = request.Amount,
                Status = request.Status,
                RequestedAtUtc = request.RequestedAtUtc,
                ProcessedAtUtc = request.ProcessedAtUtc,
                ProcessedByEmail = request.ProcessedByEmail,
                AdminNote = request.AdminNote,
            })
            .ToListAsync();
    }

    [HotChocolate.Authorization.Authorize]
    [Cost(25)]
    public async Task<List<GoldTokenWithdrawalRequestInfo>> GetGoldTokenWithdrawalRequests(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        return await db.GoldTokenWithdrawalRequests
            .AsNoTracking()
            .OrderByDescending(request => request.RequestedAtUtc)
            .Select(request => new GoldTokenWithdrawalRequestInfo
            {
                Id = request.Id,
                PlayerAccountId = request.PlayerAccountId,
                PlayerEmail = request.PlayerEmail,
                Network = request.Network,
                AssetId = request.AssetId,
                DestinationAddress = request.DestinationAddress,
                Amount = request.Amount,
                Status = request.Status,
                RequestedAtUtc = request.RequestedAtUtc,
                ProcessedAtUtc = request.ProcessedAtUtc,
                ProcessedByEmail = request.ProcessedByEmail,
                AdminNote = request.AdminNote,
            })
            .ToListAsync();
    }
}
