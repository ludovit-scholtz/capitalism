namespace Shared.Economy;

public static class MiningScarcityCalculator
{
    public const decimal UpperFullEfficiencyThreshold = 0.70m;
    public const decimal MidEfficiencyThreshold = 0.20m;
    public const decimal MidEfficiencyFloor = 0.60m;
    public const decimal MinimumEfficiencyFloor = 0.30m;

    public static decimal ComputeRemainingRatio(decimal? quantityRemaining, decimal? initialQuantity)
    {
        if (!quantityRemaining.HasValue || !initialQuantity.HasValue || initialQuantity.Value <= 0m)
        {
            return 1m;
        }

        var ratio = quantityRemaining.Value / initialQuantity.Value;
        return Math.Clamp(ratio, 0m, 1m);
    }

    public static decimal ComputeEfficiencyFactor(decimal? quantityRemaining, decimal? initialQuantity)
    {
        var ratio = ComputeRemainingRatio(quantityRemaining, initialQuantity);
        return ComputeEfficiencyFactorFromRatio(ratio);
    }

    public static decimal ComputeEfficiencyFactorFromRatio(decimal remainingRatio)
    {
        var ratio = Math.Clamp(remainingRatio, 0m, 1m);

        if (ratio > UpperFullEfficiencyThreshold)
        {
            return 1m;
        }

        if (ratio > MidEfficiencyThreshold)
        {
            var segmentProgress = (ratio - MidEfficiencyThreshold) / (UpperFullEfficiencyThreshold - MidEfficiencyThreshold);
            return MidEfficiencyFloor + (segmentProgress * (1m - MidEfficiencyFloor));
        }

        var lowSegmentProgress = ratio / MidEfficiencyThreshold;
        return MinimumEfficiencyFloor + (lowSegmentProgress * (MidEfficiencyFloor - MinimumEfficiencyFloor));
    }

    public static bool CrossedDownThreshold(decimal previousRatio, decimal currentRatio, decimal threshold)
    {
        return previousRatio > threshold && currentRatio <= threshold;
    }
}
