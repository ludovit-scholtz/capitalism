using System.Text.Json;
using Api.Data;
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
}
