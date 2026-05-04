using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// A badge earned by a player for reaching a specific milestone.
/// Unique per (PlayerId, BadgeType) — unlocking the same badge twice is a no-op.
/// </summary>
public sealed class PlayerAchievementBadge
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    [Required, MaxLength(60)]
    public string BadgeType { get; set; } = string.Empty;

    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The tick at which this badge was unlocked.</summary>
    public long UnlockedAtTick { get; set; }
}

/// <summary>Known badge type string constants.</summary>
public static class BadgeType
{
    public const string FirstMillion = "FIRST_MILLION";
    public const string Monopolist = "MONOPOLIST";
    public const string MasterTrader = "MASTER_TRADER";
    public const string PowerMagnate = "POWER_MAGNATE";
    public const string CityPioneer = "CITY_PIONEER";
    public const string ExportChampion = "EXPORT_CHAMPION";
    public const string IndustryLeader = "INDUSTRY_LEADER";
    public const string MarketDominator = "MARKET_DOMINATOR";
    public const string RankClimber = "RANK_CLIMBER";
    public const string LegendaryTycoon = "LEGENDARY_TYCOON";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        FirstMillion, Monopolist, MasterTrader, PowerMagnate, CityPioneer,
        ExportChampion, IndustryLeader, MarketDominator, RankClimber, LegendaryTycoon,
    };

    public static string GetRarity(string badgeType) => badgeType switch
    {
        LegendaryTycoon or MarketDominator => "LEGENDARY",
        PowerMagnate or IndustryLeader => "EPIC",
        MasterTrader or ExportChampion => "RARE",
        _ => "COMMON",
    };

    public static string GetUnlockCondition(string badgeType) => badgeType switch
    {
        FirstMillion => "Accumulate $1,000,000 USD in total wealth",
        Monopolist => "Own buildings in all three cities simultaneously",
        MasterTrader => "Complete 100 exchange trades",
        PowerMagnate => "Own a power plant that supplies 10+ buildings",
        CityPioneer => "Be the first player to place a building in any city",
        ExportChampion => "Sell 10,000 units of products across all companies",
        IndustryLeader => "Reach leaderboard rank #1 in any single city by revenue",
        MarketDominator => "Hold >50% market share in any product in any city for 10 consecutive ticks",
        RankClimber => "Improve leaderboard rank by 10+ positions in a single tax year",
        LegendaryTycoon => "Accumulate $100,000,000 USD in total wealth",
        _ => "Complete a special milestone",
    };
}
