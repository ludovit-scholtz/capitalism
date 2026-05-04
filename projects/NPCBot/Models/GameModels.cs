using System.Text.Json.Serialization;

namespace Capitalism.NPCBot.Models;

// ── Auth ─────────────────────────────────────────────────────────────────────

public sealed class AuthPayload
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [JsonPropertyName("player")]
    public PlayerProfile? Player { get; set; }
}

// ── Player ───────────────────────────────────────────────────────────────────

public sealed class PlayerProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("onboardingCompletedAtUtc")]
    public DateTime? OnboardingCompletedAtUtc { get; set; }

    [JsonPropertyName("onboardingCurrentStep")]
    public string? OnboardingCurrentStep { get; set; }

    [JsonPropertyName("onboardingIndustry")]
    public string? OnboardingIndustry { get; set; }

    [JsonPropertyName("onboardingCityId")]
    public string? OnboardingCityId { get; set; }

    [JsonPropertyName("onboardingCompanyId")]
    public string? OnboardingCompanyId { get; set; }

    [JsonPropertyName("onboardingFactoryLotId")]
    public string? OnboardingFactoryLotId { get; set; }

    [JsonPropertyName("onboardingShopBuildingId")]
    public string? OnboardingShopBuildingId { get; set; }

    [JsonPropertyName("companies")]
    public List<CompanySummary> Companies { get; set; } = [];
}

// ── Company ──────────────────────────────────────────────────────────────────

public sealed class CompanySummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("cash")]
    public decimal Cash { get; set; }

    [JsonPropertyName("buildings")]
    public List<BuildingSummary> Buildings { get; set; } = [];
}

// ── Building ─────────────────────────────────────────────────────────────────

public sealed class BuildingSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("cityId")]
    public string CityId { get; set; } = string.Empty;
}

// ── City ─────────────────────────────────────────────────────────────────────

public sealed class CitySummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonPropertyName("population")]
    public int Population { get; set; }
}

// ── Lots ─────────────────────────────────────────────────────────────────────

public sealed class BuildingLotSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("district")]
    public string District { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("suitableTypes")]
    public string SuitableTypes { get; set; } = string.Empty;

    [JsonPropertyName("buildingId")]
    public string? BuildingId { get; set; }
}

// ── Products ─────────────────────────────────────────────────────────────────

public sealed class ProductTypeSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("industry")]
    public string Industry { get; set; } = string.Empty;

    [JsonPropertyName("basePrice")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("isProOnly")]
    public bool IsProOnly { get; set; }
}

// ── Game state ────────────────────────────────────────────────────────────────

public sealed class GameStateSummary
{
    [JsonPropertyName("currentTick")]
    public long CurrentTick { get; set; }

    [JsonPropertyName("tickIntervalSeconds")]
    public int TickIntervalSeconds { get; set; }

    [JsonPropertyName("taxCycleTicks")]
    public int TaxCycleTicks { get; set; }
}

// ── Rankings ─────────────────────────────────────────────────────────────────

public sealed class RankingEntry
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("netWorth")]
    public decimal NetWorth { get; set; }
}
