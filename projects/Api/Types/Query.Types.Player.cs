using Api.Data.Entities;

namespace Api.Types;

/// <summary>Public profile data for a player, returned by the playerProfile query.</summary>
public sealed class PlayerProfileResult
{
    /// <summary>Player identifier.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Player display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Player profile gender.</summary>
    public string Gender { get; set; } = "UNSPECIFIED";

    /// <summary>Optional bio set by the player (max 160 chars).</summary>
    public string? Bio { get; set; }

    /// <summary>UTC timestamp when the player registered.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>In-game year when the player joined (derived from CreatedAtUtc and game tick).</summary>
    public int JoinGameYear { get; set; }

    /// <summary>Whether the player has an active Pro subscription.</summary>
    public bool HasProSubscription { get; set; }

    /// <summary>Total wealth in USD (personal cash + share portfolio).</summary>
    public decimal TotalWealthUsd { get; set; }

    /// <summary>Total company equity in USD (sum of company cash + buildings + inventory).</summary>
    public decimal TotalCompanyEquityUsd { get; set; }

    /// <summary>Number of companies owned.</summary>
    public int CompanyCount { get; set; }

    /// <summary>Global leaderboard rank (1-based, 0 if not ranked).</summary>
    public int LeaderboardRank { get; set; }

    /// <summary>Building types (industries) the player is active in.</summary>
    public List<string> ActiveBuildingTypes { get; set; } = [];

    /// <summary>Number of distinct cities where the player has buildings.</summary>
    public int CitiesWithBuildings { get; set; }

    /// <summary>Total quantity of products sold across all time.</summary>
    public decimal TotalProductsSold { get; set; }

    /// <summary>Hall-of-fame statistics for this player.</summary>
    public PlayerHallOfFame HallOfFame { get; set; } = new();
}

/// <summary>Hall-of-fame records for a player profile.</summary>
public sealed class PlayerHallOfFame
{
    /// <summary>Highest single-tick revenue recorded across all companies.</summary>
    public decimal HighestSingleTickRevenue { get; set; }

    /// <summary>Tick at which the highest single-tick revenue occurred (0 if none).</summary>
    public long HighestSingleTickRevenueTick { get; set; }

    /// <summary>Price paid in the largest single building acquisition.</summary>
    public decimal LargestBuildingAcquisitionPrice { get; set; }

    /// <summary>Name of the building acquired in the largest acquisition.</summary>
    public string? LargestBuildingAcquisitionName { get; set; }

    /// <summary>Highest brand quality (combined R&amp;D + marketing) ever achieved across all companies.</summary>
    public decimal HighestBrandQuality { get; set; }

    /// <summary>Name of the brand that reached the highest quality.</summary>
    public string? HighestBrandQualityName { get; set; }

    /// <summary>Number of game ticks the player has been active (account age in ticks).</summary>
    public long AccountAgeTicks { get; set; }
}
