namespace Capitalism.Shared.Ranking;

/// <summary>
/// Shared master ranking bounty event type codes used by game and master APIs.
/// </summary>
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
