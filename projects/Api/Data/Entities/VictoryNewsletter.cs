namespace Api.Data.Entities;

public sealed class VictoryNewsletter
{
    public Guid Id { get; set; }
    public Guid? WinnerPlayerId { get; set; }
    public string WinnerDisplayName { get; set; } = string.Empty;
    public string WinnerCompanyName { get; set; } = string.Empty;
    public decimal WinnerNetWorthUsd { get; set; }
    public string Top10RankingsJson { get; set; } = "[]";
    public int TotalFxTradeCount { get; set; }
    public decimal TotalFxVolumeUsd { get; set; }
    public decimal TotalProductsSold { get; set; }
    public int ActiveCitiesCount { get; set; }
    public long GameDurationTicks { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
