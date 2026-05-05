using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for merging a target company (≥90% ownership) into a company the player controls.</summary>
public sealed class MergeCompanyInput
{
    /// <summary>The company to absorb (player must have ≥90% combined ownership).</summary>
    public Guid TargetCompanyId { get; set; }

    /// <summary>The company that receives all transferred assets (must be directly controlled by the player).</summary>
    public Guid DestinationCompanyId { get; set; }
}

/// <summary>Input for launching an additional company via the IPO path.</summary>
public sealed class StartAdditionalCompanyInput
{
    /// <summary>Name for the new company (3–200 chars).</summary>
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>City where the new company's primary bank account will be denominated.</summary>
    public Guid CityId { get; set; }

    /// <summary>
    /// IPO raise target in USD. Supported values: 200000 (50% founder), 400000 (33.33% founder), 600000 (25% founder).
    /// When omitted, defaults to 200000.
    /// </summary>
    public decimal? IpoRaiseTarget { get; set; }
}
