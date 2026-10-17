using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using HotChocolate;
using HotChocolate.Language;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Security;

public sealed partial class ApiKeyScopeMiddleware
{
    private static ValueTask<IReadOnlyCollection<Guid>> ResolveDirectCompanyIdsAsync(
        JsonElement variables,
        AppDbContext _,
        Guid __,
        CancellationToken ___)
    {
        var ids = new List<Guid>();
        AddGuidIfPresent(ids, variables, "input", "companyId");
        AddGuidIfPresent(ids, variables, "input", "borrowerCompanyId");
        AddGuidIfPresent(ids, variables, "input", "tradeAccountCompanyId");
        return ValueTask.FromResult<IReadOnlyCollection<Guid>>(ids);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBuildingCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var buildingIds = new List<Guid>();
        AddGuidIfPresent(buildingIds, variables, "input", "buildingId");
        AddGuidIfPresent(buildingIds, variables, "input", "collateralBuildingId");
        if (buildingIds.Count == 0)
        {
            return [];
        }

        return await db.Buildings
            .AsNoTracking()
            .Where(building => buildingIds.Contains(building.Id))
            .Select(building => building.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static ValueTask<IReadOnlyCollection<Guid>> ResolveMakeOfferBuyerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
        => ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken);

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBankAccountCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var accountIds = new List<Guid>();
        AddGuidIfPresent(accountIds, variables, "input", "bankAccountId");
        AddGuidIfPresent(accountIds, variables, "input", "fromBankAccountId");
        AddGuidIfPresent(accountIds, variables, "input", "toBankAccountId");
        AddGuidIfPresent(accountIds, variables, "bankAccountId");

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await db.BankAccounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id) && account.CompanyId.HasValue)
            .Select(account => account.CompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveExchangeBuyCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var companyIds = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            companyIds.Add(bankCompanyId);
        }

        var unitIds = new List<Guid>();
        AddGuidIfPresent(unitIds, variables, "input", "targetBuildingUnitId");
        if (unitIds.Count > 0)
        {
            var unitCompanyIds = await db.BuildingUnits
                .AsNoTracking()
                .Where(unit => unitIds.Contains(unit.Id))
                .Select(unit => unit.Building.CompanyId)
                .Distinct()
                .ToListAsync(cancellationToken);
            companyIds.UnionWith(unitCompanyIds);
        }

        return companyIds.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveExchangeSellCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var companyIds = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            companyIds.Add(bankCompanyId);
        }

        var unitIds = new List<Guid>();
        AddGuidIfPresent(unitIds, variables, "input", "sourceBuildingUnitId");
        if (unitIds.Count > 0)
        {
            var unitCompanyIds = await db.BuildingUnits
                .AsNoTracking()
                .Where(unit => unitIds.Contains(unit.Id))
                .Select(unit => unit.Building.CompanyId)
                .Distinct()
                .ToListAsync(cancellationToken);
            companyIds.UnionWith(unitCompanyIds);
        }

        return companyIds.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveAcceptLoanCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var companyId in await ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        foreach (var buildingCompanyId in await ResolveBuildingCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(buildingCompanyId);
        }

        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(bankCompanyId);
        }

        return ids.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveLoanBorrowerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var loanIds = new List<Guid>();
        AddGuidIfPresent(loanIds, variables, "input", "loanId");
        if (loanIds.Count == 0)
        {
            return [];
        }

        return await db.Loans
            .AsNoTracking()
            .Where(loan => loanIds.Contains(loan.Id))
            .Select(loan => loan.BorrowerCompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBuildingOfferSellerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var offerIds = new List<Guid>();
        AddGuidIfPresent(offerIds, variables, "input", "offerId");
        if (offerIds.Count == 0)
        {
            return [];
        }

        return await db.BuildingSaleOffers
            .AsNoTracking()
            .Where(offer => offerIds.Contains(offer.Id))
            .Select(offer => offer.Building.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveTransferFundsCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(bankCompanyId);
        }

        return ids.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveForexCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveTradingAccountCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var companyId in await ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        foreach (var companyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        if (ids.Count > 0)
        {
            return ids.ToList();
        }

        return await ResolveActiveTradingCompanyIdsAsync(variables, db, playerId, cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveActiveTradingCompanyIdsAsync(
        JsonElement _,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var activeCompanyId = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == playerId && player.ActiveAccountType == AccountContextType.Company)
            .Select(player => player.ActiveCompanyId)
            .FirstOrDefaultAsync(cancellationToken);

        return activeCompanyId.HasValue ? [activeCompanyId.Value] : [];
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveLimitOrderOwnerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var orderIds = new List<Guid>();
        AddGuidIfPresent(orderIds, variables, "orderId");
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await db.LimitOrders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id) && order.OwnerCompanyId.HasValue)
            .Select(order => order.OwnerCompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static ValueTask<IReadOnlyCollection<Guid>> ResolveSwitchContextCompanyIdsAsync(
        JsonElement variables,
        AppDbContext _,
        Guid __,
        CancellationToken ___)
    {
        var accountType = GetStringPath(variables, "input", "accountType");
        if (!string.Equals(accountType, AccountContextType.Company, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyCollection<Guid>>([]);
        }

        var ids = new List<Guid>();
        AddGuidIfPresent(ids, variables, "input", "companyId");
        return ValueTask.FromResult<IReadOnlyCollection<Guid>>(ids);
    }

    private static void AddGuidIfPresent(List<Guid> target, JsonElement root, params string[] path)
    {
        var guid = GetGuidPath(root, path);
        if (guid.HasValue)
        {
            target.Add(guid.Value);
        }
    }

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

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
