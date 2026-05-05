using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

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
