namespace Api.Types;

/// <summary>Summary info about a gold AMM liquidity pool.</summary>
public sealed class GoldAmmPoolInfo
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);
    public decimal FiatReserve { get; set; }
    public decimal GoldReserve { get; set; }
    public decimal TotalLiquidityShares { get; set; }
    /// <summary>Implied gold price in fiat: FiatReserve / GoldReserve.</summary>
    public decimal ImpliedGoldPrice => GoldReserve > 0 ? Math.Round(FiatReserve / GoldReserve, 4) : 0m;
    /// <summary>The authenticated player's position in this pool, if any.</summary>
    public GoldAmmPositionInfo? MyPosition { get; set; }
}

/// <summary>A player's liquidity position in a gold AMM pool.</summary>
public sealed class GoldAmmPositionInfo
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal LiquidityShares { get; set; }
    /// <summary>This position's percentage of the total pool (0-100).</summary>
    public decimal SharePercent { get; set; }
    /// <summary>How much fiat the player can claim back from the pool.</summary>
    public decimal ClaimableFiat { get; set; }
    /// <summary>How much gold the player can claim back from the pool.</summary>
    public decimal ClaimableGold { get; set; }
    /// <summary>Original fiat provided (used for blocked-resource tracking).</summary>
    public decimal FiatProvided { get; set; }
    /// <summary>Original gold provided (used for blocked-resource tracking).</summary>
    public decimal GoldProvided { get; set; }
}

/// <summary>Quote for a gold AMM swap (preview, does not execute).</summary>
public sealed class GoldAmmSwapQuote
{
    public string Direction { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);
    public decimal InputAmount { get; set; }
    public decimal OutputAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal FeePercent { get; set; } = 1m;
    /// <summary>Implied price of 1 XAU in fiat after the swap.</summary>
    public decimal ImpliedPrice { get; set; }
    /// <summary>Slippage percentage relative to current mid-market price.</summary>
    public decimal SlippagePercent { get; set; }
    public decimal PoolFiatReserve { get; set; }
    public decimal PoolGoldReserve { get; set; }
    /// <summary>Player's available (non-blocked) balance of the input asset.</summary>
    public decimal AvailableInputBalance { get; set; }
}

/// <summary>Result of an executed gold AMM swap.</summary>
public sealed class GoldAmmSwapResult
{
    public Guid TradeId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal InputAmount { get; set; }
    public decimal OutputAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal ImpliedPrice { get; set; }
    public decimal NewFiatBalance { get; set; }
    public decimal NewGoldBalance { get; set; }
}

/// <summary>Result of creating a pool or adding liquidity.</summary>
public sealed class GoldAmmLiquidityResult
{
    public Guid PoolId { get; set; }
    public Guid PositionId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal LiquidityShares { get; set; }
    public decimal FiatProvided { get; set; }
    public decimal GoldProvided { get; set; }
    public decimal PoolFiatReserve { get; set; }
    public decimal PoolGoldReserve { get; set; }
    public decimal NewFiatBalance { get; set; }
    public decimal NewGoldBalance { get; set; }
}

/// <summary>Result of removing liquidity from a gold AMM pool.</summary>
public sealed class GoldAmmRemoveLiquidityResult
{
    public Guid PositionId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal FiatReturned { get; set; }
    public decimal GoldReturned { get; set; }
    public decimal RemainingShares { get; set; }
    public decimal NewFiatBalance { get; set; }
    public decimal NewGoldBalance { get; set; }
}

/// <summary>Player's gold (XAU) balance info.</summary>
public sealed class GoldBalanceInfo
{
    public decimal Balance { get; set; }
    public decimal BlockedInPools { get; set; }
    public decimal AvailableBalance => Balance - BlockedInPools;
}
