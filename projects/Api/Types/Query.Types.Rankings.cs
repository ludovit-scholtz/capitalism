using Api.Data.Entities;

namespace Api.Types;

/// <summary>Payload for player ranking.</summary>
public sealed class PlayerRanking
{
    /// <summary>Player identifier.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Player display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Total wealth = PersonalCash + SharesValue.
    /// See <see cref="Query.GetRankings"/> for the full valuation formula.
    /// </summary>
    public decimal TotalWealth { get; set; }

    /// <summary>
    /// TotalWealth normalized to USD for fair cross-currency leaderboard ranking.
    /// Personal settlement cash (EUR) and share values from companies in various currencies
    /// are all converted to USD using current FX rates.
    /// </summary>
    public decimal TotalWealthUsd { get; set; }

    /// <summary>Cash held in the player's personal account.</summary>
    public decimal PersonalCash { get; set; }

    /// <summary>Market value of all shares held by the player's personal account.</summary>
    public decimal SharesValue { get; set; }

    /// <summary>Number of companies owned.</summary>
    public int CompanyCount { get; set; }

    /// <summary>Unlocked profile badge types shown as compact icons in the leaderboard.</summary>
    public List<string> BadgeTypes { get; set; } = [];
}

/// <summary>Individual company ranking for the leaderboard.</summary>
public sealed class CompanyRanking
{
    /// <summary>Company identifier.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Company display name.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Owner player identifier.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Owner player display name.</summary>
    public string OwnerDisplayName { get; set; } = string.Empty;

    /// <summary>Total company wealth = Cash + BuildingValue + InventoryValue in the company's local currency.</summary>
    public decimal TotalWealth { get; set; }

    /// <summary>
    /// TotalWealth normalized to USD for fair cross-currency leaderboard comparison.
    /// </summary>
    public decimal TotalWealthUsd { get; set; }

    /// <summary>ISO 4217 currency code for this company's local cash (e.g. "EUR", "CZK", "USD").</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Cash on hand for this company.</summary>
    public decimal Cash { get; set; }

    /// <summary>Estimated value of company buildings.</summary>
    public decimal BuildingValue { get; set; }

    /// <summary>Estimated value of inventory in company buildings.</summary>
    public decimal InventoryValue { get; set; }

    /// <summary>Number of buildings owned by this company.</summary>
    public int BuildingCount { get; set; }
}
