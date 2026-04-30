using MasterApi.Data.Entities;

namespace MasterApi.Types;

public sealed class RankingSummaryInfo
{
    public decimal TotalPoints { get; set; }

    public int GlobalRank { get; set; }

    public int PreviousGlobalRank { get; set; }

    public int RankMovement { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class RankingLeaderboardEntryInfo
{
    public Guid PlayerId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public decimal TotalPoints { get; set; }

    public int GlobalRank { get; set; }

    public int RankMovement { get; set; }
}

public sealed class RankingHistoryFilterInput
{
    public string? BountyCode { get; set; }

    public string? ServerKey { get; set; }

    public string? Status { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Limit { get; set; } = 100;

    public int Offset { get; set; }
}

public sealed class RankingRewardHistoryItem
{
    public Guid Id { get; set; }

    public string BountyCode { get; set; } = string.Empty;

    public string BountyDisplayName { get; set; } = string.Empty;

    public decimal PointsAwarded { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ServerKey { get; set; }

    public DateTime EventDateUtc { get; set; }

    public DateTime AwardedAtUtc { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

public sealed class RankingBountyDefinitionInfo
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal RewardPoints { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsVisibleToPlayers { get; set; }

    public bool RequiresModeration { get; set; }

    public string CooldownMode { get; set; } = string.Empty;

    public string SourceEventType { get; set; } = string.Empty;

    public string ProofRequirement { get; set; } = string.Empty;

    public string VisibilityScope { get; set; } = string.Empty;

    public string ValidationSettingsJson { get; set; } = "{}";

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class RankingEventModerationItem
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string PlayerEmail { get; set; } = string.Empty;

    public string? ServerKey { get; set; }

    public string? ProofReference { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class RankingAdminDashboardInfo
{
    public List<RankingBountyDefinitionInfo> Bounties { get; set; } = [];

    public List<RankingEventModerationItem> PendingModerationEvents { get; set; } = [];

    public List<RankingRunInfo> RecentRuns { get; set; } = [];
}

public sealed class RankingRunInfo
{
    public Guid Id { get; set; }

    public string RunType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public int ProcessedEvents { get; set; }

    public int RewardRecordsCreated { get; set; }

    public decimal TotalPointsAwarded { get; set; }

    public decimal TotalPointsBeforeDecay { get; set; }

    public decimal TotalPointsAfterDecay { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed class IngestRankingEventInput : MasterServerServiceInput
{
    public string EventType { get; set; } = string.Empty;

    public string PlayerEmail { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public string? ExternalEventId { get; set; }

    public string? UniqueScopeKey { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public string? ProofReference { get; set; }
}

public sealed class ModerateRankingEventInput
{
    public Guid EventId { get; set; }

    public bool Approve { get; set; }

    public string? Reason { get; set; }
}

public sealed class UpsertRankingBountyDefinitionInput
{
    public Guid? Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal RewardPoints { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsVisibleToPlayers { get; set; } = true;

    public bool RequiresModeration { get; set; }

    public string CooldownMode { get; set; } = RankingCooldownMode.UtcDay;

    public string SourceEventType { get; set; } = string.Empty;

    public string ProofRequirement { get; set; } = RankingProofRequirement.None;

    public string VisibilityScope { get; set; } = RankingVisibilityScope.PlayerHistory;

    public string ValidationSettingsJson { get; set; } = "{}";
}
