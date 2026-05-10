using Shared.Economy;

namespace Api.Utilities;

public static class MineExtractionIntelligenceCalculator
{
    public static decimal? ComputeBurnRatePerTick(IEnumerable<decimal> extractedAmounts)
    {
        var values = extractedAmounts.ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var average = values.Average();
        return average > 0m ? average : 0m;
    }

    public static long? ComputeExpectedDepletionTick(long currentTick, decimal currentReserve, decimal? burnRatePerTick)
    {
        if (burnRatePerTick is null || burnRatePerTick <= 0m)
        {
            return null;
        }

        if (currentReserve <= 0m)
        {
            return currentTick - 1;
        }

        var ticksToDepletion = (long)Math.Ceiling((double)(currentReserve / burnRatePerTick.Value));
        return currentTick + ticksToDepletion;
    }

    public static long? ComputeQualityDecayInflectionTick(
        long currentTick,
        decimal currentReserve,
        decimal? originalReserve,
        decimal? burnRatePerTick,
        decimal thresholdRatio = MiningScarcityCalculator.UpperFullEfficiencyThreshold)
    {
        if (!originalReserve.HasValue || originalReserve <= 0m)
        {
            return null;
        }

        if (burnRatePerTick is null || burnRatePerTick <= 0m)
        {
            return null;
        }

        var thresholdReserve = originalReserve.Value * thresholdRatio;
        if (currentReserve <= thresholdReserve)
        {
            return currentTick;
        }

        var ticksToInflection = (long)Math.Ceiling((double)((currentReserve - thresholdReserve) / burnRatePerTick.Value));
        return currentTick + ticksToInflection;
    }
}
