namespace MasterApi.Data.Entities;

public sealed class MasterRankingEvent
{
    public Guid Id { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public PlayerAccount? PlayerAccount { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? ServerKey { get; set; }

    public string? ExternalEventId { get; set; }

    public string? UniqueScopeKey { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public string? ProofReference { get; set; }

    public string Status { get; set; } = RankingEventStatus.Pending;

    public string? ModerationReason { get; set; }

    public string? ModeratedByEmail { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public ICollection<MasterRankingRewardRecord> RewardRecords { get; set; } = [];
}

public sealed class MasterRankingRewardRecord
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public PlayerAccount PlayerAccount { get; set; } = null!;

    public Guid BountyDefinitionId { get; set; }

    public MasterRankingBountyDefinition BountyDefinition { get; set; } = null!;

    public Guid? RankingEventId { get; set; }

    public MasterRankingEvent? RankingEvent { get; set; }

    public decimal PointsAwarded { get; set; }

    public string Status { get; set; } = RankingRewardStatus.Awarded;

    public string UniquenessKey { get; set; } = string.Empty;

    public string? ServerKey { get; set; }

    public DateTime EventDateUtc { get; set; }

    public DateTime AwardedAtUtc { get; set; }

    public string AwardMetadataJson { get; set; } = "{}";
}

public sealed class MasterRankingPlayerSnapshot
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public PlayerAccount PlayerAccount { get; set; } = null!;

    public decimal TotalPoints { get; set; }

    public int GlobalRank { get; set; }

    public int PreviousGlobalRank { get; set; }

    public decimal LastDailyDecayFactorApplied { get; set; } = 1.0m;

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MasterRankingEvaluationRun
{
    public Guid Id { get; set; }

    public string RunType { get; set; } = RankingRunType.HourlyEvaluation;

    public string Status { get; set; } = RankingRunStatus.Succeeded;

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public int ProcessedEvents { get; set; }

    public int RewardRecordsCreated { get; set; }

    public decimal TotalPointsAwarded { get; set; }

    public decimal TotalPointsBeforeDecay { get; set; }

    public decimal TotalPointsAfterDecay { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed class MasterRankingBountyAudit
{
    public Guid Id { get; set; }

    public Guid BountyDefinitionId { get; set; }

    public MasterRankingBountyDefinition BountyDefinition { get; set; } = null!;

    public string ChangedByEmail { get; set; } = string.Empty;

    public string ChangeType { get; set; } = string.Empty;

    public string PreviousValueJson { get; set; } = "{}";

    public string NewValueJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}

public static class RankingEventStatus
{
    public const string Pending = "PENDING";
    public const string PendingModeration = "PENDING_MODERATION";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Processed = "PROCESSED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        PendingModeration,
        Approved,
        Rejected,
        Processed,
    };
}

public static class RankingRewardStatus
{
    public const string Awarded = "AWARDED";
    public const string Rejected = "REJECTED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Awarded,
        Rejected,
    };
}

public static class RankingRunType
{
    public const string HourlyEvaluation = "HOURLY_EVALUATION";
    public const string DailyDecay = "DAILY_DECAY";
}

public static class RankingRunStatus
{
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}
