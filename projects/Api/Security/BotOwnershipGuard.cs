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
    public const string NotOwnedOrNotFoundCode = "NOT_OWNED_OR_NOT_FOUND";

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
            throw CreateNotOwnedOrNotFoundException();
        }

        var isOwned = await db.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId.Value && company.PlayerId == playerId, cancellationToken);

        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException();
        }
    }

    public async Task EnsureBankAccountOwnedAsync(Guid playerId, Guid? bankAccountId, CancellationToken cancellationToken)
    {
        if (!bankAccountId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException();
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
            throw CreateNotOwnedOrNotFoundException();
        }
    }

    public async Task EnsureBuildingOwnedAsync(Guid playerId, Guid? buildingId, CancellationToken cancellationToken)
    {
        if (!buildingId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException();
        }

        var isOwned = await db.Buildings
            .AsNoTracking()
            .AnyAsync(building =>
                    building.Id == buildingId.Value
                    && building.Company != null
                    && building.Company.PlayerId == playerId,
                cancellationToken);

        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException();
        }
    }

    public async Task EnsureLoanOwnedAsync(Guid playerId, Guid? loanId, CancellationToken cancellationToken)
    {
        if (!loanId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException();
        }

        var isOwned = await db.Loans
            .AsNoTracking()
            .AnyAsync(loan =>
                    loan.Id == loanId.Value
                    && loan.BorrowerCompany != null
                    && loan.BorrowerCompany.PlayerId == playerId,
                cancellationToken);

        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException();
        }
    }

    public async Task EnsureBuildingOfferSellerOwnedAsync(Guid playerId, Guid? offerId, CancellationToken cancellationToken)
    {
        if (!offerId.HasValue)
        {
            throw CreateNotOwnedOrNotFoundException();
        }

        var isOwned = await db.BuildingSaleOffers
            .AsNoTracking()
            .AnyAsync(offer =>
                    offer.Id == offerId.Value
                    && offer.Building != null
                    && offer.Building.Company != null
                    && offer.Building.Company.PlayerId == playerId,
                cancellationToken);

        if (!isOwned)
        {
            throw CreateNotOwnedOrNotFoundException();
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
            throw CreateNotOwnedOrNotFoundException();
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
            throw CreateNotOwnedOrNotFoundException();
        }
    }

    private static GraphQLException CreateNotOwnedOrNotFoundException()
        => new(
            ErrorBuilder.New()
                .SetMessage("The requested company, account, loan, or offer was not found for the authenticated API-key owner.")
                .SetCode(NotOwnedOrNotFoundCode)
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
