using Api.Engine.Campaign;

namespace Api.Types;

/// <summary>The guided first-week campaign of the signed-in player (#602).</summary>
public sealed class FirstWeekCampaignResult
{
    /// <summary>False until the player has finished onboarding.</summary>
    public bool Started { get; set; }

    /// <summary>1-7 while the campaign runs (capped at 7), 0 before it started.</summary>
    public int DayNumber { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public int ClaimedCount { get; set; }
    public bool AllClaimed { get; set; }
    public List<FirstWeekGoalItem> Goals { get; set; } = [];

    public static FirstWeekCampaignResult From(FirstWeekCampaignState state) => new()
    {
        Started = state.Started,
        DayNumber = state.DayNumber,
        StartedAtUtc = state.StartedAtUtc,
        ClaimedCount = state.ClaimedCount,
        AllClaimed = state.AllClaimed,
        Goals = state.Goals.Select(g => new FirstWeekGoalItem
        {
            Code = g.Code,
            Day = g.Goal.Day,
            Progress = g.Progress,
            Target = g.Target,
            IsUnlocked = g.IsUnlocked,
            IsComplete = g.IsComplete,
            IsClaimed = g.IsClaimed,
            CanClaim = g.CanClaim,
            RewardAmount = g.Goal.RewardAmount,
            RewardCurrencyCode = FirstWeekCampaignCatalog.RewardCurrency,
        }).ToList(),
    };
}

public sealed class FirstWeekGoalItem
{
    public string Code { get; set; } = string.Empty;
    public int Day { get; set; }
    public int Progress { get; set; }
    public int Target { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsComplete { get; set; }
    public bool IsClaimed { get; set; }
    public bool CanClaim { get; set; }
    public decimal RewardAmount { get; set; }
    public string RewardCurrencyCode { get; set; } = string.Empty;
}

public sealed class ClaimCampaignRewardInput
{
    public string GoalCode { get; set; } = string.Empty;

    /// <summary>The company that receives the reward; defaults to the player's active company.</summary>
    public Guid? CompanyId { get; set; }
}

public sealed class ClaimCampaignRewardResult
{
    /// <summary>True when this call paid the reward; false when the goal had already been claimed (nothing is paid twice).</summary>
    public bool Claimed { get; set; }

    public string GoalCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
