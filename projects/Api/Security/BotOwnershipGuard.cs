using System.Text.Json;
using Api.Data;
using Api.Utilities;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Api.Security;

/// <summary>
/// Resolves bot/API-key mutation ownership strictly from the authenticated player identity,
/// never from client-supplied player identifiers.
/// </summary>
public sealed class BotOwnershipGuard(AppDbContext db)
{
    public const string NotFoundOrNotOwnedCode = "NOT_FOUND_OR_NOT_OWNED";
    public const string AuthorizationReasonNotFound = "NOT_FOUND";
    public const string AuthorizationReasonNotOwned = "NOT_OWNED";

    public async Task EnsureMutationOwnershipAsync(
        string operationName,
        JsonElement variables,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        switch (operationName)
        {
            case "executeForexSwap":
                await EnsureBankAccountOwnedIfPresentAsync(playerId, GetGuidPath(variables, "input", "fromBankAccountId"), cancellationToken);
                await EnsureBankAccountOwnedIfPresentAsync(playerId, GetGuidPath(variables, "input", "toBankAccountId"), cancellationToken);
                break;

            case "buyShares":
            case "sellShares":
                if (GetGuidPath(variables, "input", "tradeAccountCompanyId") is Guid tradeAccountCompanyId)
                {
                    await EnsureCompanyOwnedAsync(playerId, tradeAccountCompanyId, cancellationToken);
                }

                await EnsureBankAccountOwnedIfPresentAsync(playerId, GetGuidPath(variables, "input", "bankAccountId"), cancellationToken);
                break;

            case "replaceCEO":
                await EnsureCompanyOwnedAsync(playerId, GetGuidPath(variables, "input", "companyId"), cancellationToken);
                break;

            case "acceptLoan":
                var borrowerCompanyId = GetGuidPath(variables, "input", "borrowerCompanyId");
                await EnsureCompanyOwnedAsync(playerId, borrowerCompanyId, cancellationToken);

                if (GetGuidPath(variables, "input", "collateralBuildingId") is Guid collateralBuildingId)
                {
                    await EnsureBuildingOwnedAsync(playerId, collateralBuildingId, cancellationToken);
                }

                if (GetGuidPath(variables, "input", "bankAccountId") is Guid settlementAccountId)
                {
                    await EnsureCompanyBankAccountOwnedAsync(playerId, borrowerCompanyId, settlementAccountId, cancellationToken);
                }
                break;

            case "repayLoanDebt":
                await EnsureLoanOwnedAsync(playerId, GetGuidPath(variables, "input", "loanId"), cancellationToken);
                break;

            case "setBuildingForSale":
            case "destroyBuilding":
                await EnsureBuildingOwnedAsync(playerId, GetGuidPath(variables, "input", "buildingId"), cancellationToken);
                break;

            case "createGoldAmmPool":
            case "executeGoldAmmSwap":
                break;

            case "addGoldAmmLiquidity":
                await EnsureGoldAmmPoolOwnedAsync(playerId, GetGuidPath(variables, "input", "poolId"), cancellationToken);
                break;

            case "removeGoldAmmLiquidity":
                await EnsureGoldAmmPositionOwnedAsync(playerId, GetGuidPath(variables, "input", "positionId"), cancellationToken);
                break;

            case "placeLimitOrder":
                await EnsureLimitOrderPlacementOwnershipAsync(playerId, GetStringPath(variables, "input", "stockSymbol"), cancellationToken);
                break;

            case "cancelLimitOrder":
                await EnsureLimitOrderOwnedAsync(playerId, GetGuidPath(variables, "orderId"), cancellationToken);
                break;

            case "proposeDividend":
                await EnsureDividendProposalAuthorizationAsync(playerId, GetStringPath(variables, "input", "stockSymbol"), cancellationToken);
                break;

            case "voteDividend":
            case "voteDividendProposal":
                await EnsureDividendProposalOwnershipAsync(playerId, GetGuidPath(variables, "input", "proposalId"), cancellationToken);
                break;

            case "makeOfferOnBuilding":
                await EnsureCompanyOwnedAsync(playerId, GetGuidPath(variables, "input", "buyerCompanyId"), cancellationToken);
                break;

            case "acceptBuildingOffer":
            case "cancelBuildingOffer":
                await EnsureBuildingOfferSellerOwnedAsync(playerId, GetGuidPath(variables, "input", "offerId"), cancellationToken);
                break;
        }
    }

    public async Task EnsureCompanyOwnedAsync(Guid playerId, Guid? companyId, CancellationToken cancellationToken)
    {
        if (!companyId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownerId = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId.Value)
            .Select(company => (Guid?)company.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId != playerId)
        {
            throw CreateNotOwnedOrNotFoundException(
                ownerId is null ? AuthorizationReasonNotFound : AuthorizationReasonNotOwned,
                companyId);
        }
    }

    public async Task EnsureBankAccountOwnedAsync(Guid playerId, Guid? bankAccountId, CancellationToken cancellationToken)
    {
        if (!bankAccountId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var isOwned = await db.BankAccounts
            .AsNoTracking()
            .AnyAsync(account =>
                    account.Id == bankAccountId.Value
                    && (account.PlayerId == playerId
                        || (account.CompanyId.HasValue && account.Company != null && account.Company.PlayerId == playerId)),
                cancellationToken);

        if (!isOwned)
        {
            var exists = await db.BankAccounts
                .AsNoTracking()
                .AnyAsync(account => account.Id == bankAccountId.Value, cancellationToken);
            throw CreateNotOwnedOrNotFoundException(
                exists ? AuthorizationReasonNotOwned : AuthorizationReasonNotFound,
                bankAccountId);
        }
    }

    public async Task EnsureBuildingOwnedAsync(Guid playerId, Guid? buildingId, CancellationToken cancellationToken)
    {
        if (!buildingId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownerId = await db.Buildings
            .AsNoTracking()
            .Where(building => building.Id == buildingId.Value)
            .Select(building => (Guid?)building.Company!.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId != playerId)
        {
            throw CreateNotOwnedOrNotFoundException(
                ownerId is null ? AuthorizationReasonNotFound : AuthorizationReasonNotOwned,
                buildingId);
        }
    }

    public async Task EnsureLoanOwnedAsync(Guid playerId, Guid? loanId, CancellationToken cancellationToken)
    {
        if (!loanId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownerId = await db.Loans
            .AsNoTracking()
            .Where(loan => loan.Id == loanId.Value)
            .Select(loan => (Guid?)loan.BorrowerCompany!.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId != playerId)
        {
            throw CreateNotOwnedOrNotFoundException(
                ownerId is null ? AuthorizationReasonNotFound : AuthorizationReasonNotOwned,
                loanId);
        }
    }

    public async Task EnsureBuildingOfferSellerOwnedAsync(Guid playerId, Guid? offerId, CancellationToken cancellationToken)
    {
        if (!offerId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownerId = await db.BuildingSaleOffers
            .AsNoTracking()
            .Where(offer => offer.Id == offerId.Value)
            .Select(offer => (Guid?)offer.Building!.Company!.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId != playerId)
        {
            throw CreateNotOwnedOrNotFoundException(
                ownerId is null ? AuthorizationReasonNotFound : AuthorizationReasonNotOwned,
                offerId);
        }
    }

    private async Task EnsureBankAccountOwnedIfPresentAsync(Guid playerId, Guid? bankAccountId, CancellationToken cancellationToken)
    {
        if (bankAccountId.HasValue)
        {
            await EnsureBankAccountOwnedAsync(playerId, bankAccountId.Value, cancellationToken);
        }
    }

    private async Task EnsureCompanyBankAccountOwnedAsync(
        Guid playerId,
        Guid? companyId,
        Guid? bankAccountId,
        CancellationToken cancellationToken)
    {
        if (!companyId.HasValue || !bankAccountId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var isOwned = await db.BankAccounts
            .AsNoTracking()
            .AnyAsync(account =>
                    account.Id == bankAccountId.Value
                    && account.CompanyId == companyId.Value
                    && account.Company != null
                    && account.Company.PlayerId == playerId,
                cancellationToken);

        if (!isOwned)
        {
            var exists = await db.BankAccounts
                .AsNoTracking()
                .AnyAsync(account => account.Id == bankAccountId.Value, cancellationToken);
            throw CreateNotOwnedOrNotFoundException(
                exists ? AuthorizationReasonNotOwned : AuthorizationReasonNotFound,
                bankAccountId);
        }
    }

    private async Task EnsureGoldAmmPoolOwnedAsync(Guid playerId, Guid? poolId, CancellationToken cancellationToken)
    {
        if (!poolId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var poolExists = await db.GoldAmmPools
            .AsNoTracking()
            .AnyAsync(pool => pool.Id == poolId.Value, cancellationToken);
        if (!poolExists)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, poolId);
        }

        var isOwned = await db.GoldAmmPositions
            .AsNoTracking()
            .AnyAsync(position => position.PoolId == poolId.Value && position.PlayerId == playerId, cancellationToken);
        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotOwned, poolId);
        }
    }

    private async Task EnsureGoldAmmPositionOwnedAsync(Guid playerId, Guid? positionId, CancellationToken cancellationToken)
    {
        if (!positionId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownerId = await db.GoldAmmPositions
            .AsNoTracking()
            .Where(position => position.Id == positionId.Value)
            .Select(position => (Guid?)position.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerId != playerId)
        {
            throw CreateNotOwnedOrNotFoundException(
                ownerId is null ? AuthorizationReasonNotFound : AuthorizationReasonNotOwned,
                positionId);
        }
    }

    private async Task EnsureLimitOrderPlacementOwnershipAsync(Guid playerId, string? stockSymbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stockSymbol) || !StockSymbolCodec.TryParseCompanyId(stockSymbol, out var companyId))
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var hasOwnedUsdSettlementAccount = await db.BankAccounts
            .AsNoTracking()
            .AnyAsync(account =>
                    account.CurrencyCode == "USD"
                    && account.ClosedAtUtc == null
                    && (account.PlayerId == playerId
                        || (account.CompanyId.HasValue && account.Company != null && account.Company.PlayerId == playerId)),
                cancellationToken);
        if (!hasOwnedUsdSettlementAccount)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var companyExists = await db.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId, cancellationToken);
        if (!companyExists)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, companyId);
        }
    }

    private async Task EnsureLimitOrderOwnedAsync(Guid playerId, Guid? orderId, CancellationToken cancellationToken)
    {
        if (!orderId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var ownershipState = await db.LimitOrders
            .AsNoTracking()
            .Where(order => order.Id == orderId.Value)
            .Select(order => new
            {
                order.OwnerPlayerId,
                OwnerCompanyPlayerId = order.OwnerCompany != null ? (Guid?)order.OwnerCompany.PlayerId : null,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (ownershipState is null)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, orderId);
        }

        var isOwned = ownershipState.OwnerPlayerId == playerId
            || ownershipState.OwnerCompanyPlayerId == playerId;
        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotOwned, orderId);
        }
    }

    private async Task EnsureDividendProposalAuthorizationAsync(Guid playerId, string? stockSymbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stockSymbol) || !StockSymbolCodec.TryParseCompanyId(stockSymbol, out var companyId))
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        await EnsureDividendCompanyStakeOwnedAsync(playerId, companyId, cancellationToken);
    }

    private async Task EnsureDividendProposalOwnershipAsync(Guid playerId, Guid? proposalId, CancellationToken cancellationToken)
    {
        if (!proposalId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, null);
        }

        var proposalCompanyId = await db.DividendProposals
            .AsNoTracking()
            .Where(proposal => proposal.Id == proposalId.Value)
            .Select(proposal => (Guid?)proposal.CompanyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!proposalCompanyId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, proposalId);
        }

        await EnsureDividendCompanyStakeOwnedAsync(playerId, proposalCompanyId.Value, cancellationToken, proposalId);
    }

    private async Task EnsureDividendCompanyStakeOwnedAsync(
        Guid playerId,
        Guid companyId,
        CancellationToken cancellationToken,
        Guid? attemptedObjectId = null)
    {
        var ownerId = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => (Guid?)company.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!ownerId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotFound, attemptedObjectId ?? companyId);
        }

        if (ownerId == playerId)
        {
            return;
        }

        var controlledCompanyIds = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == playerId)
            .Select(company => company.Id)
            .ToListAsync(cancellationToken);

        var hasShares = await db.Shareholdings
            .AsNoTracking()
            .AnyAsync(holding =>
                    holding.CompanyId == companyId
                    && holding.ShareCount > 0m
                    && (holding.OwnerPlayerId == playerId
                        || (holding.OwnerCompanyId.HasValue && controlledCompanyIds.Contains(holding.OwnerCompanyId.Value))),
                cancellationToken);

        if (!hasShares)
        {
            throw CreateNotOwnedOrNotFoundException(AuthorizationReasonNotOwned, attemptedObjectId ?? companyId);
        }
    }

    private static GraphQLException CreateNotOwnedOrNotFoundException(string reason, Guid? attemptedObjectId)
        => new(
            ErrorBuilder.New()
                .SetMessage("The requested company, account, loan, or offer was not found for the authenticated API-key owner.")
                .SetCode(NotFoundOrNotOwnedCode)
                .SetExtension("authorizationReason", reason)
                .SetExtension("attemptedObjectId", attemptedObjectId?.ToString())
                .Build());

    private static Guid? GetGuidPath(JsonElement root, params string[] path)
    {
        var element = root;
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var value)
            ? value
            : null;
    }

    private static string? GetStringPath(JsonElement root, params string[] path)
    {
        var element = root;
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }
}
