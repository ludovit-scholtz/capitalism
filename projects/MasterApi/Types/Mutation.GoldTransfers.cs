using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    [HotChocolate.Authorization.Authorize]
    public async Task<GoldTokenDepositRequestInfo> CreateGoldTokenDepositRequest(
        CreateGoldTokenDepositRequestInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GoldTokenTransferOptions> transferOptions)
    {
        EnsurePositiveGoldAmount(input.Amount);
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var network = NormalizeGoldNetwork(input.Network);
        var assetId = ResolveAssetIdByNetwork(network);
        var depositAddress = network == "VOI"
            ? transferOptions.Value.VoiDepositAddress.Trim()
            : transferOptions.Value.AlgorandDepositAddress.Trim();

        var request = new GoldTokenDepositRequest
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Network = network,
            AssetId = assetId,
            DepositAddress = depositAddress,
            SenderAddress = string.IsNullOrWhiteSpace(input.SenderAddress) ? null : input.SenderAddress.Trim(),
            Amount = decimal.Round(input.Amount, 8, MidpointRounding.AwayFromZero),
            Status = "PENDING",
            RequestedAtUtc = DateTime.UtcNow,
        };

        db.GoldTokenDepositRequests.Add(request);
        request.NoteText = $"CAP-{request.Id}";
        await db.SaveChangesAsync();

        return new GoldTokenDepositRequestInfo
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
            NoteText = request.NoteText,
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<GoldTokenWithdrawalRequestInfo> CreateGoldTokenWithdrawalRequest(
        CreateGoldTokenWithdrawalRequestInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        EnsurePositiveGoldAmount(input.Amount);
        if (string.IsNullOrWhiteSpace(input.DestinationAddress))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Destination address is required.")
                    .SetCode("DESTINATION_ADDRESS_REQUIRED")
                    .Build());
        }

        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        EnsureSufficientGoldBalance(
            player,
            input.Amount,
            $"Not enough tokenized gold for withdrawal request. Requested {input.Amount:0.########} g, available {player.GoldTokenBalance:0.########} g.");

        var network = NormalizeGoldNetwork(input.Network);
        var assetId = ResolveAssetIdByNetwork(network);

        var request = new GoldTokenWithdrawalRequest
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Network = network,
            AssetId = assetId,
            DestinationAddress = input.DestinationAddress.Trim(),
            Amount = decimal.Round(input.Amount, 8, MidpointRounding.AwayFromZero),
            Status = "PENDING",
            RequestedAtUtc = DateTime.UtcNow,
        };

        var balanceBefore = player.GoldTokenBalance;
        ApplyGoldDebit(player, request.Amount);
        AddSystemGoldTransaction(
            db,
            player,
            -request.Amount,
            balanceBefore,
            $"WITHDRAWAL_REQUEST_CREATED:{request.Network}:{request.Id}");

        db.GoldTokenWithdrawalRequests.Add(request);
        await db.SaveChangesAsync();

        return new GoldTokenWithdrawalRequestInfo
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
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<GoldTokenDepositRequestInfo> ProcessGoldTokenDepositRequest(
        ProcessGoldTokenDepositRequestInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var request = await db.GoldTokenDepositRequests
            .FirstOrDefaultAsync(item => item.Id == input.RequestId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Deposit request not found.")
                    .SetCode("DEPOSIT_REQUEST_NOT_FOUND")
                    .Build());

        if (request.Status != "PENDING")
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only pending deposit requests can be processed.")
                    .SetCode("DEPOSIT_REQUEST_NOT_PENDING")
                    .Build());
        }

        var player = await db.PlayerAccounts
            .FirstOrDefaultAsync(item => item.Id == request.PlayerAccountId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var balanceBefore = player.GoldTokenBalance;
        player.GoldTokenBalance += request.Amount;
        player.ConcurrencyToken = Guid.NewGuid();

        request.Status = "PROCESSED";
        request.ProcessedAtUtc = DateTime.UtcNow;
        request.ProcessedByEmail = callerEmail;
        request.AdminNote = string.IsNullOrWhiteSpace(input.AdminNote) ? null : input.AdminNote.Trim();

        db.GoldTokenTransactions.Add(new GoldTokenTransaction
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = player.GoldTokenBalance,
            AdminEmail = callerEmail,
            Note = $"DEPOSIT_PROCESSED:{request.Network}:{request.Id}",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        return new GoldTokenDepositRequestInfo
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
            NoteText = request.NoteText,
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<GoldTokenWithdrawalRequestInfo> ProcessGoldTokenWithdrawalRequest(
            ProcessGoldTokenWithdrawalRequestInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var request = await db.GoldTokenWithdrawalRequests
            .FirstOrDefaultAsync(item => item.Id == input.RequestId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Withdrawal request not found.")
                    .SetCode("WITHDRAWAL_REQUEST_NOT_FOUND")
                    .Build());

        if (request.Status != "PENDING")
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only pending withdrawal requests can be processed.")
                    .SetCode("WITHDRAWAL_REQUEST_NOT_PENDING")
                    .Build());
        }

        var player = await db.PlayerAccounts
            .FirstOrDefaultAsync(item => item.Id == request.PlayerAccountId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        request.Status = "PROCESSED";
        request.ProcessedAtUtc = DateTime.UtcNow;
        request.ProcessedByEmail = callerEmail;
        request.AdminNote = string.IsNullOrWhiteSpace(input.AdminNote) ? null : input.AdminNote.Trim();

        await db.SaveChangesAsync();

        return new GoldTokenWithdrawalRequestInfo
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
        };
    }
}
