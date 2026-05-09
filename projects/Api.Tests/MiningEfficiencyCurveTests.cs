using Shared.Economy;

namespace Api.Tests;

public sealed class MiningEfficiencyCurveTests
{
    [Theory]
    [InlineData(100, 100, 1.00)]
    [InlineData(70, 100, 1.00)]
    [InlineData(20, 100, 0.60)]
    [InlineData(5, 100, 0.375)]
    [InlineData(0, 100, 0.30)]
    public void ComputeEfficiencyFactor_UsesExpectedThresholds(decimal remaining, decimal original, decimal expected)
    {
        var actual = MiningScarcityCalculator.ComputeEfficiencyFactor(remaining, original);
        Assert.Equal(expected, decimal.Round(actual, 3));
    }

    [Fact]
    public void ComputeEfficiencyFactor_WithMissingInputs_DefaultsToFullEfficiency()
    {
        Assert.Equal(1m, MiningScarcityCalculator.ComputeEfficiencyFactor(null, 100m));
        Assert.Equal(1m, MiningScarcityCalculator.ComputeEfficiencyFactor(100m, null));
        Assert.Equal(1m, MiningScarcityCalculator.ComputeEfficiencyFactor(100m, 0m));
    }
}
