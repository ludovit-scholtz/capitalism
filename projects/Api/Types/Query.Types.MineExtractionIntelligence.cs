namespace Api.Types;

public sealed class MineExtractionDailyPoint
{
    public int DayIndex { get; set; }
    public decimal ExtractedAmount { get; set; }
    public decimal EfficiencyPercent { get; set; }
    public decimal ReserveRemaining { get; set; }
}

public sealed class MineExtractionIntelligence
{
    public List<MineExtractionDailyPoint> DailyExtraction { get; set; } = [];
    public decimal? BurnRatePerTick { get; set; }
    public decimal? BurnRatePerDay { get; set; }
    public long? ExpectedDepletionTick { get; set; }
    public long? QualityDecayInflectionTick { get; set; }
    public decimal? EstimatedGameDaysRemaining { get; set; }
    public decimal? CurrentReserve { get; set; }
    public decimal? OriginalReserve { get; set; }
    public long CurrentTick { get; set; }
}
