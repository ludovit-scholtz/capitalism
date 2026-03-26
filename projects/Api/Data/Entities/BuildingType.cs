namespace Api.Data.Entities;

/// <summary>
/// Defines the types of buildings that can be constructed in the game.
/// Each type has different unit grid capabilities.
/// </summary>
public static class BuildingType
{
    /// <summary>Extracts raw materials from the ground.</summary>
    public const string Mine = "MINE";

    /// <summary>Manufactures products from raw materials.</summary>
    public const string Factory = "FACTORY";

    /// <summary>Sells products directly to the public.</summary>
    public const string SalesShop = "SALES_SHOP";

    /// <summary>Researches product quality and brand quality improvements.</summary>
    public const string ResearchDevelopment = "RESEARCH_DEVELOPMENT";

    /// <summary>Residential building that earns rent revenue.</summary>
    public const string Apartment = "APARTMENT";

    /// <summary>Commercial office building that earns rent revenue.</summary>
    public const string Commercial = "COMMERCIAL";

    /// <summary>Newspaper, Radio, or TV station that improves brand quality.</summary>
    public const string MediaHouse = "MEDIA_HOUSE";

    /// <summary>Allows lending money to other players.</summary>
    public const string Bank = "BANK";

    /// <summary>Commodity exchange for trading raw materials and products.</summary>
    public const string Exchange = "EXCHANGE";

    /// <summary>Generates electricity required by all buildings.</summary>
    public const string PowerPlant = "POWER_PLANT";

    /// <summary>All valid building types.</summary>
    public static readonly string[] All =
    [
        Mine, Factory, SalesShop, ResearchDevelopment,
        Apartment, Commercial, MediaHouse, Bank, Exchange, PowerPlant
    ];
}
