using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

/// <summary>
/// Result of purging a single player's game data from the shard.
/// </summary>
public sealed record PlayerAccountPurgeResult(
    bool PlayerFound,
    int CompaniesRemoved,
    int BuildingsDestroyed,
    int BanksTransferredToGovernment);

/// <summary>
/// Removes all of a player's game data from this game server when their master
/// account is deleted. Banks owned by the player are transferred to the government
/// (with public 0% deposit / 20% lending rates) so depositors keep their accounts;
/// every other building is destroyed and the player record is removed.
/// </summary>
public sealed class PlayerAccountPurgeService(AppDbContext db)
{
    private const decimal GovernmentDepositRatePercent = 0m;
    private const decimal GovernmentLendingRatePercent = 20m;

    public async Task<PlayerAccountPurgeResult> PurgeAsync(string playerEmail, CancellationToken cancellationToken)
    {
        var normalizedEmail = playerEmail.Trim().ToLowerInvariant();

        // The government system actor must never be purged.
        if (string.Equals(normalizedEmail, GovernmentActorConstants.GovernmentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerAccountPurgeResult(false, 0, 0, 0);
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedEmail, cancellationToken);
        if (player is null)
        {
            return new PlayerAccountPurgeResult(false, 0, 0, 0);
        }

        var governmentCompanyId = await ResolveGovernmentCompanyIdAsync(cancellationToken);

        var companies = await db.Companies
            .Where(company => company.PlayerId == player.Id)
            .ToListAsync(cancellationToken);
        var companyIds = companies.Select(company => company.Id).ToHashSet();

        var buildings = await db.Buildings
            .Where(building => companyIds.Contains(building.CompanyId))
            .ToListAsync(cancellationToken);

        var bankBuildings = buildings.Where(building => building.Type == BuildingType.Bank).ToList();
        var nonBankBuildings = buildings.Where(building => building.Type != BuildingType.Bank).ToList();
        var nonBankBuildingIds = nonBankBuildings.Select(building => building.Id).ToHashSet();

        await TransferBanksToGovernmentAsync(bankBuildings, companyIds, governmentCompanyId, cancellationToken);
        await DestroyNonBankBuildingsAsync(nonBankBuildingIds, cancellationToken);
        await RemoveCompanyScopedDataAsync(companyIds, cancellationToken);

        db.Buildings.RemoveRange(nonBankBuildings);
        db.Companies.RemoveRange(companies);
        db.Players.Remove(player);

        await db.SaveChangesAsync(cancellationToken);

        return new PlayerAccountPurgeResult(
            PlayerFound: true,
            CompaniesRemoved: companies.Count,
            BuildingsDestroyed: nonBankBuildings.Count,
            BanksTransferredToGovernment: bankBuildings.Count);
    }

    private async Task<Guid?> ResolveGovernmentCompanyIdAsync(CancellationToken cancellationToken)
    {
        return await db.Companies
            .Where(company => company.Player.Email == GovernmentActorConstants.GovernmentEmail)
            .Select(company => (Guid?)company.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task TransferBanksToGovernmentAsync(
        IReadOnlyCollection<Building> bankBuildings,
        IReadOnlySet<Guid> companyIds,
        Guid? governmentCompanyId,
        CancellationToken cancellationToken)
    {
        if (bankBuildings.Count == 0 || governmentCompanyId is null)
        {
            return;
        }

        var bankBuildingIds = bankBuildings.Select(building => building.Id).ToHashSet();

        foreach (var bank in bankBuildings)
        {
            bank.CompanyId = governmentCompanyId.Value;
            bank.IsGovernmentOwned = true;
            bank.DepositInterestRatePercent = GovernmentDepositRatePercent;
            bank.LendingInterestRatePercent = GovernmentLendingRatePercent;
            bank.PendingDepositInterestRatePercent = null;
            bank.PendingDepositRateEffectiveTick = null;
        }

        // Reassign the bank's own operating/base-capital accounts to the government so the
        // transferred bank stays solvent. Depositor accounts (owned by other players) keep
        // pointing at the now-government bank building and are untouched.
        var bankOwnedAccounts = await db.BankAccounts
            .Where(account => account.BankBuildingId != null
                && bankBuildingIds.Contains(account.BankBuildingId.Value)
                && account.CompanyId != null
                && companyIds.Contains(account.CompanyId.Value))
            .ToListAsync(cancellationToken);
        foreach (var account in bankOwnedAccounts)
        {
            account.CompanyId = governmentCompanyId.Value;
            account.IsGovernmentAccount = true;
        }

        // Loan offers issued by the transferred bank now belong to the government.
        var loanOffers = await db.LoanOffers
            .Where(offer => bankBuildingIds.Contains(offer.BankBuildingId)
                && companyIds.Contains(offer.LenderCompanyId))
            .ToListAsync(cancellationToken);
        foreach (var offer in loanOffers)
        {
            offer.LenderCompanyId = governmentCompanyId.Value;
        }

        var loans = await db.Loans
            .Where(loan => bankBuildingIds.Contains(loan.BankBuildingId)
                && companyIds.Contains(loan.LenderCompanyId))
            .ToListAsync(cancellationToken);
        foreach (var loan in loans)
        {
            loan.LenderCompanyId = governmentCompanyId.Value;
        }
    }

    private async Task DestroyNonBankBuildingsAsync(
        IReadOnlySet<Guid> buildingIds,
        CancellationToken cancellationToken)
    {
        if (buildingIds.Count == 0)
        {
            return;
        }

        // Remove dependents that do not cascade or are restricted on Building deletion.
        var salesRecords = await db.PublicSalesRecords
            .Where(record => buildingIds.Contains(record.BuildingId))
            .ToListAsync(cancellationToken);
        db.PublicSalesRecords.RemoveRange(salesRecords);

        var inventories = await db.Inventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .ToListAsync(cancellationToken);
        db.Inventories.RemoveRange(inventories);

        var units = await db.BuildingUnits
            .Where(unit => buildingIds.Contains(unit.BuildingId))
            .ToListAsync(cancellationToken);
        db.BuildingUnits.RemoveRange(units);

        var energyListings = await db.EnergyListings
            .Where(listing => buildingIds.Contains(listing.BuildingId))
            .ToListAsync(cancellationToken);
        db.EnergyListings.RemoveRange(energyListings);

        // Detach destroyed buildings from any loan collateral references.
        var collateralLoans = await db.Loans
            .Where(loan => loan.CollateralBuildingId != null && buildingIds.Contains(loan.CollateralBuildingId.Value))
            .ToListAsync(cancellationToken);
        foreach (var loan in collateralLoans)
        {
            loan.CollateralBuildingId = null;
        }
    }

    private async Task RemoveCompanyScopedDataAsync(
        IReadOnlySet<Guid> companyIds,
        CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0)
        {
            return;
        }

        // Loans and loan offers borrowed/issued by the purged companies.
        var loans = await db.Loans
            .Where(loan => companyIds.Contains(loan.BorrowerCompanyId) || companyIds.Contains(loan.LenderCompanyId))
            .ToListAsync(cancellationToken);
        db.Loans.RemoveRange(loans);

        var loanOffers = await db.LoanOffers
            .Where(offer => companyIds.Contains(offer.LenderCompanyId))
            .ToListAsync(cancellationToken);
        db.LoanOffers.RemoveRange(loanOffers);

        // Government / supply contracts referencing the purged companies.
        var supplyContracts = await db.SupplyContracts
            .Where(contract => companyIds.Contains(contract.SellerCompanyId) || companyIds.Contains(contract.BuyerCompanyId))
            .ToListAsync(cancellationToken);
        db.SupplyContracts.RemoveRange(supplyContracts);

        var fulfillments = await db.ContractFulfillments
            .Where(fulfillment => companyIds.Contains(fulfillment.CompanyId))
            .ToListAsync(cancellationToken);
        db.ContractFulfillments.RemoveRange(fulfillments);

        var bids = await db.ContractBids
            .Where(bid => companyIds.Contains(bid.CompanyId))
            .ToListAsync(cancellationToken);
        db.ContractBids.RemoveRange(bids);

        // Public sales records owned by the purged companies (restricted on Company deletion).
        var salesRecords = await db.PublicSalesRecords
            .Where(record => companyIds.Contains(record.CompanyId))
            .ToListAsync(cancellationToken);
        db.PublicSalesRecords.RemoveRange(salesRecords);

        // Media / brand campaigns that target the purged companies (restricted otherwise).
        var mediaUnits = await db.MediaHouseUnits
            .Where(unit => companyIds.Contains(unit.TargetCompanyId))
            .ToListAsync(cancellationToken);
        db.MediaHouseUnits.RemoveRange(mediaUnits);

        var brandQualityRecords = await db.BrandQualityRecords
            .Where(record => companyIds.Contains(record.TargetCompanyId))
            .ToListAsync(cancellationToken);
        db.BrandQualityRecords.RemoveRange(brandQualityRecords);

        // Shareholdings in or owned by the purged companies.
        var shareholdings = await db.Shareholdings
            .Where(holding => companyIds.Contains(holding.CompanyId)
                || (holding.OwnerCompanyId != null && companyIds.Contains(holding.OwnerCompanyId.Value)))
            .ToListAsync(cancellationToken);
        db.Shareholdings.RemoveRange(shareholdings);

        // Operating bank accounts of the purged companies (deposits at others' banks included).
        var companyAccounts = await db.BankAccounts
            .Where(account => account.CompanyId != null && companyIds.Contains(account.CompanyId.Value))
            .ToListAsync(cancellationToken);
        db.BankAccounts.RemoveRange(companyAccounts);
    }
}
