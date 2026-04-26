using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for player registration.</summary>
public sealed class RegisterInput
{
    /// <summary>Email address for the new account.</summary>
    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name shown in game.</summary>
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Password (minimum 8 characters).</summary>
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Input for player login.</summary>
public sealed class LoginInput
{
    /// <summary>Email address.</summary>
    [Required]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Input for sending a shared in-game chat message.</summary>
public sealed class SendChatMessageInput
{
    /// <summary>Plain-text message body shown in the shared chat feed.</summary>
    [Required, MaxLength(300)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Input for creating a new company.</summary>
public sealed class CreateCompanyInput
{
    /// <summary>Company name.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Input for updating company profile and salary settings.</summary>
public sealed class UpdateCompanySettingsInput
{
    public Guid CompanyId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Portion of post-tax annual profit paid out as dividends. 0.2 means 20%.</summary>
    public decimal? DividendPayoutRatio { get; set; }

    [Required]
    public List<CompanyCitySalarySettingInput> CitySalarySettings { get; set; } = [];
}

public sealed class CompanyCitySalarySettingInput
{
    public Guid CityId { get; set; }
    public decimal SalaryMultiplier { get; set; }
}

/// <summary>Input for placing a building on the map.</summary>
public sealed class PlaceBuildingInput
{
    /// <summary>Company that will own the building.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>City where the building is placed.</summary>
    public Guid CityId { get; set; }

    /// <summary>Building type (MINE, FACTORY, SALES_SHOP, etc.).</summary>
    [Required, MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the building.
    /// Optional — when empty or omitted, a natural name is auto-generated.
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>First product to manufacture (for onboarding factory setup).</summary>
    public Guid? InitialProductTypeId { get; set; }

    /// <summary>Media type for MEDIA_HOUSE buildings: NEWSPAPER, RADIO, TV. Required when Type is MEDIA_HOUSE.</summary>
    [MaxLength(20)]
    public string? MediaType { get; set; }
}

/// <summary>Input for selecting onboarding choices.</summary>
public sealed class OnboardingInput
{
    /// <summary>Industry the player wants to start with: FURNITURE, FOOD_PROCESSING, HEALTHCARE.</summary>
    [Required, MaxLength(50)]
    public string Industry { get; set; } = string.Empty;

    /// <summary>City where the player wants to start.</summary>
    public Guid CityId { get; set; }

    /// <summary>First product type to manufacture.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Name for the player's first company.</summary>
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;
}

/// <summary>Input for starting lot-based onboarding by purchasing the first factory lot.</summary>
public sealed class StartOnboardingCompanyInput
{
    /// <summary>Industry the player wants to start with: FURNITURE, FOOD_PROCESSING, HEALTHCARE.</summary>
    [Required, MaxLength(50)]
    public string Industry { get; set; } = string.Empty;

    /// <summary>City where the player wants to start.</summary>
    public Guid CityId { get; set; }

    /// <summary>Name for the player's first company.</summary>
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Factory-capable lot chosen for the first building.</summary>
    public Guid FactoryLotId { get; set; }

    /// <summary>
    /// Optional IPO raise target for the starter company. Supported values: 400000, 600000, 800000.
    /// When omitted, the onboarding flow defaults to the 400000 raise / 50% founder-share option.
    /// </summary>
    public decimal? IpoRaiseTarget { get; set; }
}

/// <summary>Input for finishing lot-based onboarding by choosing a starter product and first shop lot.</summary>
public sealed class FinishOnboardingInput
{
    /// <summary>First product type to manufacture and sell.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Sales-shop-capable lot chosen for the first retail building.</summary>
    public Guid ShopLotId { get; set; }
}

/// <summary>Input for switching the authenticated player's active acting account.</summary>
public sealed class SwitchAccountContextInput
{
    [Required, MaxLength(20)]
    public string AccountType { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }
}

/// <summary>Input for purchasing company shares from the public stock exchange.</summary>
public sealed class BuySharesInput
{
    public Guid CompanyId { get; set; }

    public decimal ShareCount { get; set; }

    /// <summary>Optional: override the server-side active account. Accepted values are "PERSON" or "COMPANY".</summary>
    [MaxLength(10)]
    public string? TradeAccountType { get; set; }

    /// <summary>Optional: company ID to trade from when TradeAccountType is "COMPANY".</summary>
    public Guid? TradeAccountCompanyId { get; set; }

    /// <summary>
    /// Required settlement account for this trade. Must belong to the active trade account
    /// (person/company) and be denominated in USD.
    /// </summary>
    public Guid? BankAccountId { get; set; }
}

/// <summary>Input for selling company shares back to the public stock exchange.</summary>
public sealed class SellSharesInput
{
    public Guid CompanyId { get; set; }

    public decimal ShareCount { get; set; }

    /// <summary>Optional: override the server-side active account. Accepted values are "PERSON" or "COMPANY".</summary>
    [MaxLength(10)]
    public string? TradeAccountType { get; set; }

    /// <summary>Optional: company ID to trade from when TradeAccountType is "COMPANY".</summary>
    public Guid? TradeAccountCompanyId { get; set; }

    /// <summary>
    /// Required settlement account for this trade. Must belong to the active trade account
    /// (person/company) and be denominated in USD.
    /// </summary>
    public Guid? BankAccountId { get; set; }
}

/// <summary>Input for storing a queued building configuration update.</summary>
public sealed class StoreBuildingConfigurationInput
{
    /// <summary>Building that should receive the queued configuration.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Queued unit layout snapshot. Server-controlled fields such as level and activation tick are not accepted.</summary>
    [Required]
    public List<BuildingConfigurationUnitInput> Units { get; set; } = [];
}

/// <summary>Input for cancelling a queued building configuration plan with rollback timing.</summary>
public sealed class CancelBuildingConfigurationInput
{
    /// <summary>Building whose pending configuration should be cancelled.</summary>
    public Guid BuildingId { get; set; }
}

/// <summary>Input for setting a building's for-sale status.</summary>
public sealed class SetBuildingForSaleInput
{
    /// <summary>Building to update.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Whether the building is listed for sale.</summary>
    public bool IsForSale { get; set; }

    /// <summary>Asking price (required when IsForSale is true).</summary>
    public decimal? AskingPrice { get; set; }
}

/// <summary>Input for setting the rent per m² on an apartment or commercial building.</summary>
public sealed class SetRentPerSqmInput
{
    /// <summary>Apartment or commercial building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>New rent per m² to apply after one in-game day (24 ticks).</summary>
    public decimal RentPerSqm { get; set; }
}

/// <summary>Input for setting the per-tick content spending budget for a media house building.</summary>
public sealed class SetMediaHouseContentBudgetInput
{
    /// <summary>MEDIA_HOUSE building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Amount to spend on content per tick.
    /// Set to null or 0 to stop content investment.
    /// Must be non-negative.
    /// </summary>
    public decimal? ContentBudgetPerTick { get; set; }
}

/// <summary>Input for purchasing a building lot and placing a building on it.</summary>
public sealed class PurchaseLotInput
{
    /// <summary>Company that will purchase the lot and own the building.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Building lot to purchase.</summary>
    public Guid LotId { get; set; }

    /// <summary>Building type to place on the lot (must be one of the lot's suitable types).</summary>
    [Required, MaxLength(30)]
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the new building.
    /// Optional — when empty or omitted, a natural name is auto-generated
    /// from the building type and a sequential number (e.g. "Factory #2").
    /// </summary>
    [MaxLength(200)]
    public string? BuildingName { get; set; }

    /// <summary>
    /// Power plant subtype (COAL, GAS, SOLAR, WIND, NUCLEAR).
    /// Required when BuildingType is POWER_PLANT; ignored otherwise.
    /// </summary>
    [MaxLength(20)]
    public string? PowerPlantType { get; set; }

    /// <summary>
    /// Media house channel type: NEWSPAPER, RADIO, TV.
    /// Required when BuildingType is MEDIA_HOUSE; ignored otherwise.
    /// </summary>
    [MaxLength(20)]
    public string? MediaType { get; set; }
}

/// <summary>User-editable portion of a building unit configuration.</summary>
public sealed class BuildingConfigurationUnitInput
{
    /// <summary>Unit type for the grid cell.</summary>
    [Required, MaxLength(30)]
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>Whether the link to the unit above is active.</summary>
    public bool LinkUp { get; set; }

    /// <summary>Whether the link to the unit below is active.</summary>
    public bool LinkDown { get; set; }

    /// <summary>Whether the link to the unit on the left is active.</summary>
    public bool LinkLeft { get; set; }

    /// <summary>Whether the link to the unit on the right is active.</summary>
    public bool LinkRight { get; set; }

    /// <summary>Whether the diagonal link to the unit above-left is active.</summary>
    public bool LinkUpLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit above-right is active.</summary>
    public bool LinkUpRight { get; set; }

    /// <summary>Whether the diagonal link to the unit below-left is active.</summary>
    public bool LinkDownLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit below-right is active.</summary>
    public bool LinkDownRight { get; set; }

    // ── Unit-specific configuration ──

    /// <summary>Resource type this unit works with (optional, for Mining/Purchase/Storage/B2B Sales).</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Product type this unit works with (optional, for Manufacturing/Purchase/Public Sales/Branding).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Minimum selling price (for B2B Sales or Public Sales units).</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Maximum purchase price (for Purchase units).</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Purchase source: EXCHANGE, LOCAL, OPTIMAL.</summary>
    [MaxLength(20)]
    public string? PurchaseSource { get; set; }

    /// <summary>Visibility: PUBLIC, COMPANY, GROUP.</summary>
    [MaxLength(20)]
    public string? SaleVisibility { get; set; }

    /// <summary>Marketing budget per tick (for Marketing units).</summary>
    public decimal? Budget { get; set; }

    /// <summary>Media house building ID (for Marketing units).</summary>
    public Guid? MediaHouseBuildingId { get; set; }

    /// <summary>Minimum product quality for Purchase units (0.0-1.0).</summary>
    public decimal? MinQuality { get; set; }

    /// <summary>Brand scope: PRODUCT, CATEGORY, COMPANY (for Branding units).</summary>
    [MaxLength(20)]
    public string? BrandScope { get; set; }

    /// <summary>Lock purchases to a specific vendor company ID (for Purchase units).</summary>
    public Guid? VendorLockCompanyId { get; set; }

    /// <summary>Lock exchange purchases to a specific source city ID (for Purchase units with EXCHANGE source).</summary>
    public Guid? LockedCityId { get; set; }

    /// <summary>
    /// Industry category for BRAND_QUALITY units with CATEGORY scope (e.g. "FURNITURE", "FOOD_PROCESSING").
    /// When provided, brand research targets this industry category directly.
    /// </summary>
    [MaxLength(50)]
    public string? IndustryCategory { get; set; }
}

/// <summary>Input for publishing a new loan offer from a bank building.</summary>
public sealed class PublishLoanOfferInput
{
    /// <summary>The bank building that will publish the offer.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Annual interest rate as a percentage (e.g. 12.5 = 12.5%). Must be between 0.1 and 200.</summary>
    public decimal AnnualInterestRatePercent { get; set; }

    /// <summary>Maximum principal any single borrower can take (must be >= 1000).</summary>
    public decimal MaxPrincipalPerLoan { get; set; }

    /// <summary>Total capital committed to this offer across all borrowers (must be >= MaxPrincipalPerLoan).</summary>
    public decimal TotalCapacity { get; set; }

    /// <summary>Repayment duration in ticks (must be between 24 and 87600, i.e. 1 in-game day to 10 in-game years).</summary>
    public long DurationTicks { get; set; }
}

/// <summary>Input for updating an existing loan offer.</summary>
public sealed class UpdateLoanOfferInput
{
    /// <summary>ID of the loan offer to update.</summary>
    public Guid LoanOfferId { get; set; }

    /// <summary>Updated annual interest rate. Must be between 0.1 and 200.</summary>
    public decimal? AnnualInterestRatePercent { get; set; }

    /// <summary>Updated maximum principal per loan.</summary>
    public decimal? MaxPrincipalPerLoan { get; set; }

    /// <summary>Updated total capacity.</summary>
    public decimal? TotalCapacity { get; set; }

    /// <summary>Updated duration in ticks.</summary>
    public long? DurationTicks { get; set; }

    /// <summary>Whether the offer should be active (visible to borrowers).</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Input for accepting a loan offer.</summary>
public sealed class AcceptLoanInput
{
    /// <summary>The loan offer to accept.</summary>
    public Guid LoanOfferId { get; set; }

    /// <summary>The company that will borrow the money.</summary>
    public Guid BorrowerCompanyId { get; set; }

    /// <summary>Principal amount to borrow (must be <= offer MaxPrincipalPerLoan and <= remaining capacity).</summary>
    public decimal PrincipalAmount { get; set; }

    /// <summary>
    /// Optional duration in ticks for direct bank borrowing.
    /// When omitted, the backend uses its default loan duration.
    /// </summary>
    public long? DurationTicks { get; set; }

    /// <summary>
    /// Optional: ID of a building owned by the borrower to pledge as collateral.
    /// When provided, the loan is secured and the principal is capped at 70% of the
    /// building's appraised value minus any existing secured exposure on the same asset.
    /// </summary>
    public Guid? CollateralBuildingId { get; set; }
}

/// <summary>Input for instantly updating the minimum sale price on a PUBLIC_SALES unit.</summary>
public sealed class UpdatePublicSalesPriceInput
{
    /// <summary>The PUBLIC_SALES building unit to update.</summary>
    public Guid UnitId { get; set; }

    /// <summary>New minimum sale price per unit. Must be greater than zero.</summary>
    public decimal NewMinPrice { get; set; }
}

/// <summary>Input for flushing inventory from a storage-capable building unit.</summary>
public sealed class FlushStorageInput
{
    /// <summary>The building unit whose inventory should be discarded.</summary>
    public Guid BuildingUnitId { get; set; }
}

/// <summary>Input for scheduling a level upgrade on a building unit.</summary>
public sealed class ScheduleUnitUpgradeInput
{
    /// <summary>The building unit to upgrade.</summary>
    public Guid UnitId { get; set; }
}

public sealed class StartAdminImpersonationInput
{
    public Guid TargetPlayerId { get; set; }

    [Required, MaxLength(20)]
    public string AccountType { get; set; } = AccountContextType.Person;

    public Guid? CompanyId { get; set; }
}

public sealed class SetPlayerInvisibleInChatInput
{
    public Guid PlayerId { get; set; }

    public bool IsInvisibleInChat { get; set; }
}

public sealed class SetLocalGameAdminRoleInput
{
    public Guid PlayerId { get; set; }

    public bool IsAdmin { get; set; }
}

public sealed class ManageGlobalGameAdminRoleInput
{
    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class GameNewsLocalizationInput
{
    [Required, MaxLength(10)]
    public string Locale { get; set; } = "en";

    [Required, MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;
}

public sealed class UpsertGameNewsEntryInput
{
    public Guid? EntryId { get; set; }

    [Required, MaxLength(20)]
    public string EntryType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required]
    public List<GameNewsLocalizationInput> Localizations { get; set; } = [];
}

public sealed class MarkGameNewsReadInput
{
    [Required]
    public List<Guid> EntryIds { get; set; } = [];
}

/// <summary>Input for merging a target company (≥90% ownership) into a company the player controls.</summary>
public sealed class MergeCompanyInput
{
    /// <summary>The company to absorb (player must have ≥90% combined ownership).</summary>
    public Guid TargetCompanyId { get; set; }

    /// <summary>The company that receives all transferred assets (must be directly controlled by the player).</summary>
    public Guid DestinationCompanyId { get; set; }
}

/// <summary>Input for opening a bank account at a bank building.</summary>
public sealed class OpenBankAccountInput
{
    /// <summary>The bank building where the account is opened.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>
    /// Optional company opening the account (must be owned by the authenticated player).
    /// When omitted, the authenticated player's personal account context is used.
    /// </summary>
    public Guid? DepositorCompanyId { get; set; }

    /// <summary>Initial account balance. Zero is allowed and is the default onboarding flow.</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for withdrawing from or fully closing a bank account.</summary>
public sealed class CloseBankAccountInput
{
    /// <summary>The account record to withdraw from.</summary>
    public Guid DepositId { get; set; }

    /// <summary>Amount to withdraw. Pass the full balance for a complete account closure.</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for adding funds to an existing bank deposit (top-up).</summary>
public sealed class TopUpDepositInput
{
    /// <summary>The existing deposit to add funds to.</summary>
    public Guid DepositId { get; set; }

    /// <summary>Amount to add (must be >= 1,000).</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for configuring a bank's deposit and lending interest rates.</summary>
public sealed class SetBankRatesInput
{
    /// <summary>The bank building to configure (must be owned by the authenticated player's company).</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Annual interest rate (%) to pay depositors. Must be between 0 and 100.</summary>
    public decimal DepositInterestRatePercent { get; set; }

    /// <summary>Annual interest rate (%) to charge borrowers. Must be between 0.1 and 200.</summary>
    public decimal LendingInterestRatePercent { get; set; }
}

/// <summary>Input for requesting a forex swap quote without executing the trade.</summary>
public sealed class GetForexQuoteInput
{
    /// <summary>Currency the player wants to sell (ISO 4217 code, e.g. "EUR").</summary>
    [Required, MaxLength(3)]
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Currency the player wants to buy (ISO 4217 code, e.g. "CZK").</summary>
    [Required, MaxLength(3)]
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount in the source currency to swap (must be > 0).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional: ID of the company bank account to debit from.
    /// When provided, the quote uses this account's balance for affordability checks.
    /// When null, falls back to the player's personal currency wallet.
    /// </summary>
    public Guid? FromBankAccountId { get; set; }

    /// <summary>
    /// Optional: ID of the company bank account to credit into.
    /// When provided, the currency of this account must match <see cref="ToCurrencyCode"/>.
    /// When null, falls back to the player's personal currency wallet.
    /// </summary>
    public Guid? ToBankAccountId { get; set; }
}

/// <summary>Input for executing a forex currency swap.</summary>
public sealed class ExecuteForexSwapInput
{
    /// <summary>Currency the player wants to sell (ISO 4217 code, e.g. "EUR").</summary>
    [Required, MaxLength(3)]
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Currency the player wants to buy (ISO 4217 code, e.g. "CZK").</summary>
    [Required, MaxLength(3)]
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount in the source currency to swap (must be > 0).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional: ID of the company bank account to debit from.
    /// When provided, funds are drawn from this bank account instead of the player's personal EUR/currency wallet.
    /// The account's <c>CurrencyCode</c> must match <see cref="FromCurrencyCode"/>.
    /// </summary>
    public Guid? FromBankAccountId { get; set; }

    /// <summary>
    /// Optional: ID of the company bank account to credit the swapped amount into.
    /// When provided, proceeds are deposited into this bank account instead of the player's personal currency wallet.
    /// The account's <c>CurrencyCode</c> must match <see cref="ToCurrencyCode"/>.
    /// </summary>
    public Guid? ToBankAccountId { get; set; }
}

/// <summary>Input for getting a gold AMM swap quote.</summary>
public sealed class GetGoldAmmSwapQuoteInput
{
    /// <summary>"FIAT_TO_GOLD" or "GOLD_TO_FIAT".</summary>
    [Required]
    public string Direction { get; set; } = string.Empty;

    /// <summary>Fiat currency code (e.g. "EUR"). Do not use "XAU" here.</summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount to swap (of the input asset).</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for executing a gold AMM swap.</summary>
public sealed class ExecuteGoldAmmSwapInput
{
    [Required]
    public string Direction { get; set; } = string.Empty;
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Minimum acceptable output amount (slippage guard). 0 = no limit.</summary>
    public decimal MinOutputAmount { get; set; }
}

/// <summary>Input for creating a new gold AMM liquidity pool.</summary>
public sealed class CreateGoldAmmPoolInput
{
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Amount of fiat currency to seed the pool with.</summary>
    public decimal FiatAmount { get; set; }
    /// <summary>Amount of gold (XAU) to seed the pool with.</summary>
    public decimal GoldAmount { get; set; }
}

/// <summary>Input for adding liquidity to an existing gold AMM pool.</summary>
public sealed class AddGoldAmmLiquidityInput
{
    /// <summary>ID of the pool to add liquidity to.</summary>
    public Guid PoolId { get; set; }
    /// <summary>Fiat amount to add. Gold amount is determined by the current pool ratio.</summary>
    public decimal FiatAmount { get; set; }
    /// <summary>Maximum gold the player is willing to spend (slippage guard).</summary>
    public decimal MaxGoldAmount { get; set; }
}

/// <summary>Input for removing liquidity from a gold AMM position.</summary>
public sealed class RemoveGoldAmmLiquidityInput
{
    /// <summary>ID of the position to remove from.</summary>
    public Guid PositionId { get; set; }
    /// <summary>Fraction of shares to remove, 0.0 to 1.0 (1.0 = remove all).</summary>
    public decimal ShareFraction { get; set; }
}

/// <summary>Input for admin setting a player's gold balance.</summary>
public sealed class AdminSetPlayerGoldBalanceInput
{
    [Required]
    public string PlayerEmail { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    [Required]
    public string Note { get; set; } = string.Empty;
}

/// <summary>Input for funding a building's assigned bank account from company cash.</summary>
public sealed class FundBuildingBankAccountInput
{
    /// <summary>The building whose bank account to fund.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Amount to transfer from company cash into the building's bank account.
    /// Must be positive and not exceed the company's available cash.
    /// </summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for assigning a different bank account to a building.</summary>
public sealed class AssignBuildingBankAccountInput
{
    /// <summary>The building to update.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// The bank account to assign to this building.
    /// Must be owned by the building's company and must have the same currency as the building's city.
    /// </summary>
    public Guid BankAccountId { get; set; }
}

/// <summary>Input for creating a new bank account for a company.</summary>
public sealed class CreateCompanyBankAccountInput
{
    /// <summary>The company that will own the new account.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// ISO 4217 currency code for the account (e.g. "EUR", "CZK").
    /// Must match a city currency available in this game server.
    /// </summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
}

/// <summary>
/// Input for transferring funds between two of the authenticated player's bank accounts.
/// Both accounts must be owned by companies the player owns and must use the same currency.
/// Cross-currency transfers must go through the Forex Exchange swap flow.
/// </summary>
public sealed class TransferFundsInput
{
    /// <summary>Source bank account ID. Must be owned by a company the caller owns.</summary>
    public Guid FromBankAccountId { get; set; }

    /// <summary>Destination bank account ID. Must be owned by a company the caller owns.</summary>
    public Guid ToBankAccountId { get; set; }

    /// <summary>
    /// Amount to transfer in the shared account currency. Must be positive and not exceed
    /// the source account balance.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>Optional human-readable description shown on both bank statement entries.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}
