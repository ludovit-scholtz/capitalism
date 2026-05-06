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

    /// <summary>Optional referral code applied during registration. Maximum 20 characters.</summary>
    [MaxLength(20)]
    public string? ReferralCode { get; set; }
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
    /// Optional IPO raise target for the starter company. Supported values: 200000, 400000, 600000.
    /// When omitted, the onboarding flow defaults to the 200000 raise / 50% founder-share option.
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
