using System.ComponentModel.DataAnnotations;

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

/// <summary>Input for creating a new company.</summary>
public sealed class CreateCompanyInput
{
    /// <summary>Company name.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
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

    /// <summary>Display name for the building.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>First product to manufacture (for onboarding factory setup).</summary>
    public Guid? InitialProductTypeId { get; set; }
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
