namespace Api.Types;

public sealed class PlayerNotificationItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? BuildingUnitId { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? LoanId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? TitleKey { get; set; }
    public string? BodyKey { get; set; }
    public string? BodyParamsJson { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}

public sealed class PlayerNotificationInbox
{
    public int UnreadCount { get; set; }
    public List<PlayerNotificationItem> Items { get; set; } = [];
}

public sealed class BankAccountAlertThresholdResult
{
    public Guid BankAccountId { get; set; }
    public decimal? AlertMinBalanceThreshold { get; set; }
}

public sealed class PublicSalesAlertThresholdResult
{
    public Guid BuildingUnitId { get; set; }
    public decimal? LowInventoryAlertThreshold { get; set; }
}

public sealed class NotificationEdge
{
    public string Cursor { get; set; } = string.Empty;
    public PlayerNotificationItem Node { get; set; } = new();
}

public sealed class NotificationPageInfo
{
    public bool HasNextPage { get; set; }
    public string? EndCursor { get; set; }
}

public sealed class NotificationConnection
{
    public int TotalCount { get; set; }
    public List<NotificationEdge> Edges { get; set; } = [];
    public NotificationPageInfo PageInfo { get; set; } = new();
}
