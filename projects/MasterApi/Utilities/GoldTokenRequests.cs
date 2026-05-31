using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Types;

namespace MasterApi.Utilities;

/// <summary>
/// Canonical creation logic for tokenized-gold deposit and withdrawal requests, shared by the
/// GraphQL mutations (<see cref="MasterApi.Types.Mutation"/>) and the Discord bot so both
/// surfaces stay byte-for-byte consistent (note format, rounding, ledger entries, balance debit).
/// </summary>
public static class GoldTokenRequests
{
    public static string NormalizeNetwork(string? network)
    {
        var normalized = network?.Trim().ToUpperInvariant();
        return normalized is "VOI" or "ALGORAND" ? normalized : throw NetworkError();
    }

    public static long ResolveAssetIdByNetwork(string network)
        => network == "VOI" ? Mutation.VoiTokenizedGoldAssetId : Mutation.AlgorandTokenizedGoldAssetId;

    public static void EnsurePositiveAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Amount must be greater than zero.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }
    }

    /// <summary>
    /// Builds and tracks a pending deposit request for the player. The caller is responsible for
    /// calling <c>SaveChangesAsync</c>. The note the depositor must include is <c>CAP-{Id}</c>.
    /// </summary>
    public static GoldTokenDepositRequest CreateDepositRequest(
        MasterDbContext db,
        PlayerAccount player,
        string? network,
        string? senderAddress,
        decimal amount,
        GoldTokenTransferOptions transferOptions)
    {
        var normalizedNetwork = NormalizeNetwork(network);
        var depositAddress = normalizedNetwork == "VOI"
            ? transferOptions.VoiDepositAddress.Trim()
            : transferOptions.AlgorandDepositAddress.Trim();

        var request = new GoldTokenDepositRequest
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Network = normalizedNetwork,
            AssetId = ResolveAssetIdByNetwork(normalizedNetwork),
            DepositAddress = depositAddress,
            SenderAddress = string.IsNullOrWhiteSpace(senderAddress) ? null : senderAddress.Trim(),
            Amount = amount <= 0m ? 0m : decimal.Round(amount, 8, MidpointRounding.AwayFromZero),
            Status = "PENDING",
            RequestedAtUtc = DateTime.UtcNow,
        };

        db.GoldTokenDepositRequests.Add(request);
        request.NoteText = $"CAP-{request.Id}";
        return request;
    }

    /// <summary>
    /// Builds and tracks a pending withdrawal request, debiting the player's balance and recording
    /// the system ledger entry. The caller is responsible for calling <c>SaveChangesAsync</c>.
    /// </summary>
    public static GoldTokenWithdrawalRequest CreateWithdrawalRequest(
        MasterDbContext db,
        PlayerAccount player,
        string? network,
        decimal amount,
        string destinationAddress)
    {
        EnsurePositiveAmount(amount);
        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Destination address is required.")
                    .SetCode("DESTINATION_ADDRESS_REQUIRED")
                    .Build());
        }

        if (player.GoldTokenBalance < amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Not enough tokenized gold for withdrawal request. Requested {amount:0.########} g, available {player.GoldTokenBalance:0.########} g.")
                    .SetCode("INSUFFICIENT_GOLD_BALANCE")
                    .Build());
        }

        var normalizedNetwork = NormalizeNetwork(network);
        var roundedAmount = decimal.Round(amount, 8, MidpointRounding.AwayFromZero);

        var request = new GoldTokenWithdrawalRequest
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Network = normalizedNetwork,
            AssetId = ResolveAssetIdByNetwork(normalizedNetwork),
            DestinationAddress = destinationAddress.Trim(),
            Amount = roundedAmount,
            Status = "PENDING",
            RequestedAtUtc = DateTime.UtcNow,
        };

        var balanceBefore = player.GoldTokenBalance;
        player.GoldTokenBalance -= roundedAmount;
        player.ConcurrencyToken = Guid.NewGuid();

        db.GoldTokenTransactions.Add(new GoldTokenTransaction
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Amount = -roundedAmount,
            BalanceBefore = balanceBefore,
            BalanceAfter = player.GoldTokenBalance,
            AdminEmail = "system@capitalism.master",
            Note = $"WITHDRAWAL_REQUEST_CREATED:{request.Network}:{request.Id}",
            CreatedAtUtc = DateTime.UtcNow,
        });

        db.GoldTokenWithdrawalRequests.Add(request);
        return request;
    }

    private static GraphQLException NetworkError()
        => new(ErrorBuilder.New()
            .SetMessage("Network must be VOI or ALGORAND.")
            .SetCode("INVALID_NETWORK")
            .Build());
}
