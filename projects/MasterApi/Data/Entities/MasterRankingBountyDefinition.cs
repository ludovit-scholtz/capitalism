namespace MasterApi.Data.Entities;

public sealed class MasterRankingBountyDefinition
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal RewardPoints { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsVisibleToPlayers { get; set; } = true;

    public bool RequiresModeration { get; set; }

    public string CooldownMode { get; set; } = RankingCooldownMode.UtcDay;

    public string SourceEventType { get; set; } = string.Empty;

    public string ProofRequirement { get; set; } = RankingProofRequirement.None;

    public string ValidationSettingsJson { get; set; } = "{}";

    public string VisibilityScope { get; set; } = RankingVisibilityScope.PlayerHistory;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<MasterRankingRewardRecord> RewardRecords { get; set; } = [];

    public ICollection<MasterRankingBountyAudit> AuditTrail { get; set; } = [];
}

public static class MasterRankingBountyCodes
{
    public const string GameImprover = "GAME_IMPROVER";
    public const string RecommendFriend = "RECOMMEND_FRIEND";
    public const string RecommendGoodFriend = "RECOMMEND_GOOD_FRIEND";
    public const string RetweetXPost = "RETWEET_X_POST";
    public const string DiscordPlayer = "DISCORD_PLAYER";
    public const string LoginToGame = "LOGIN_TO_GAME";
    public const string Manufacturer = "MANUFACTURER";
    public const string Wholesaler = "WHOLESALER";
    public const string Researcher = "RESEARCHER";
    public const string RealEstateMagnate = "REAL_ESTATE_MAGNATE";
    public const string MediaOwner = "MEDIA_OWNER";
    public const string Banker = "BANKER";
    public const string Lender = "LENDER";
    public const string FxTrader = "FX_TRADER";
    public const string StockTrader = "STOCK_TRADER";
    public const string EnergyTrader = "ENERGY_TRADER";
    public const string GoodEmployer = "GOOD_EMPLOYER";
    public const string DividendsMaster = "DIVIDENDS_MASTER";
    public const string TopPlayer = "TOP_PLAYER";
    public const string GreatPlayer = "GREAT_PLAYER";
    public const string CompanyMaster = "COMPANY_MASTER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        GameImprover,
        RecommendFriend,
        RecommendGoodFriend,
        RetweetXPost,
        DiscordPlayer,
        LoginToGame,
        Manufacturer,
        Wholesaler,
        Researcher,
        RealEstateMagnate,
        MediaOwner,
        Banker,
        Lender,
        FxTrader,
        StockTrader,
        EnergyTrader,
        GoodEmployer,
        DividendsMaster,
        TopPlayer,
        GreatPlayer,
        CompanyMaster,
    };
}

public static class RankingCooldownMode
{
    public const string None = "NONE";
    public const string UtcDay = "UTC_DAY";
    public const string UtcDayPerServer = "UTC_DAY_PER_SERVER";
    public const string Once = "ONCE";
    public const string PerUniqueKey = "PER_UNIQUE_KEY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        UtcDay,
        UtcDayPerServer,
        Once,
        PerUniqueKey,
    };
}

public static class RankingProofRequirement
{
    public const string None = "NONE";
    public const string Url = "URL";
    public const string DiscordHandle = "DISCORD_HANDLE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        Url,
        DiscordHandle,
    };
}

public static class RankingVisibilityScope
{
    public const string PlayerHistory = "PLAYER_HISTORY";
    public const string AdminOnly = "ADMIN_ONLY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PlayerHistory,
        AdminOnly,
    };
}
