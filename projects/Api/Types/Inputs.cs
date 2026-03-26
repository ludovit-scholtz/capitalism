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

/// <summary>Input for storing a queued building configuration update.</summary>
public sealed class StoreBuildingConfigurationInput
{
    /// <summary>Building that should receive the queued configuration.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Queued unit layout snapshot. Server-controlled fields such as level and activation tick are not accepted.</summary>
    [Required]
    public List<BuildingConfigurationUnitInput> Units { get; set; } = [];
}

/// <summary>User-editable portion of a building unit configuration.</summary>
public sealed class BuildingConfigurationUnitInput
{
    /// <summary>Unit type for the grid cell.</summary>
    [Required, MaxLength(30)]
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>Whether the link to the unit above is active.</summary>
    public bool LinkUp { get; set; }

    /// <summary>Whether the link to the unit below is active.</summary>
    public bool LinkDown { get; set; }

    /// <summary>Whether the link to the unit on the left is active.</summary>
    public bool LinkLeft { get; set; }

    /// <summary>Whether the link to the unit on the right is active.</summary>
    public bool LinkRight { get; set; }

    /// <summary>Whether the diagonal link to the unit above-left is active.</summary>
    public bool LinkUpLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit above-right is active.</summary>
    public bool LinkUpRight { get; set; }

    /// <summary>Whether the diagonal link to the unit below-left is active.</summary>
    public bool LinkDownLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit below-right is active.</summary>
    public bool LinkDownRight { get; set; }
}
