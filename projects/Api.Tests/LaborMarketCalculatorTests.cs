using Shared.Economy;

namespace Api.Tests;

public sealed class LaborMarketCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(1_000_000, 2_250)] // 1,000,000 x 0.45 participation x 0.005 simulated share
    [InlineData(475_000, 1_068.75)]
    public void ComputeLaborPool_ScalesWithPopulation(int population, double expected)
    {
        Assert.Equal((decimal)expected, LaborMarketCalculator.ComputeLaborPool(population));
    }

    [Fact]
    public void Compute_WithZeroPopulation_HasNoPoolAndFullUnemploymentIsNotDividedByZero()
    {
        var state = LaborMarketCalculator.Compute(population: 0, laborDemand: 0m);

        Assert.Equal(0m, state.LaborPool);
        Assert.Equal(0m, state.Utilization);
        Assert.Equal(0m, state.UnemploymentRate);
        Assert.Equal(1m, state.WageIndex);
        Assert.Equal(1m, state.StaffingFillRate);
    }

    [Fact]
    public void Compute_WithDemandButNoPopulation_IsFullyUnderstaffedAtMaxWagePressure()
    {
        var state = LaborMarketCalculator.Compute(population: 0, laborDemand: 50m);

        Assert.Equal(0m, state.UnemploymentRate);
        Assert.Equal(LaborMarketCalculator.MaxWageIndex, state.WageIndex);
        Assert.Equal(0m, state.StaffingFillRate);
    }

    [Fact]
    public void Compute_IdleCity_HasHighUnemploymentAndSlightlyLowerWages()
    {
        var pool = LaborMarketCalculator.ComputeLaborPool(1_000_000);

        var state = LaborMarketCalculator.Compute(1_000_000, laborDemand: pool * 0.10m);

        Assert.Equal(0.90m, state.UnemploymentRate);
        Assert.InRange(state.WageIndex, LaborMarketCalculator.MinWageIndex, 0.99m);
        Assert.Equal(1m, state.StaffingFillRate);
    }

    [Fact]
    public void Compute_ComfortableMiddle_HasNeutralWages()
    {
        var pool = LaborMarketCalculator.ComputeLaborPool(1_000_000);

        var state = LaborMarketCalculator.Compute(1_000_000, laborDemand: pool * 0.60m);

        Assert.Equal(1m, state.WageIndex);
        Assert.Equal(0.40m, state.UnemploymentRate);
    }

    [Fact]
    public void Compute_FullEmployment_RaisesWagesAndHasNoUnemployment()
    {
        var pool = LaborMarketCalculator.ComputeLaborPool(1_000_000);

        var state = LaborMarketCalculator.Compute(1_000_000, laborDemand: pool);

        Assert.Equal(0m, state.UnemploymentRate);
        Assert.Equal(LaborMarketCalculator.MaxWageIndex, state.WageIndex);
        Assert.Equal(1m, state.StaffingFillRate);
    }

    [Fact]
    public void Compute_Overdemand_LeavesPositionsUnfilledProportionally()
    {
        var pool = LaborMarketCalculator.ComputeLaborPool(1_000_000);

        var state = LaborMarketCalculator.Compute(1_000_000, laborDemand: pool * 2m);

        Assert.Equal(0.5m, state.StaffingFillRate);
        Assert.Equal(LaborMarketCalculator.MaxWageIndex, state.WageIndex);
    }

    [Fact]
    public void WageIndex_IsMonotonicallyNonDecreasingInUtilization()
    {
        var previous = 0m;
        for (var utilization = 0m; utilization <= 1.5m; utilization += 0.05m)
        {
            var index = LaborMarketCalculator.ComputeWageIndex(utilization);
            Assert.True(index >= previous, $"wage index fell at utilization {utilization}");
            Assert.InRange(index, LaborMarketCalculator.MinWageIndex, LaborMarketCalculator.MaxWageIndex);
            previous = index;
        }
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(0.8, 1.0, 0.8)]
    [InlineData(1.2, 1.0, 1.2)]
    [InlineData(1.0, 0.0, 1.0)] // no market average yet -> neutral
    public void ComputePayRatio_ComparesCompanyPayWithCityAverage(double company, double average, double expected)
    {
        Assert.Equal((decimal)expected, LaborMarketCalculator.ComputePayRatio((decimal)company, (decimal)average));
    }

    [Theory]
    [InlineData(1.00, false)]
    [InlineData(0.86, false)]
    [InlineData(0.84, true)]
    [InlineData(0.50, true)]
    public void IsStrikeProne_WhenPayTrailsTheCityAverage(double ratio, bool expected)
    {
        Assert.Equal(expected, LaborMarketCalculator.IsStrikeProne((decimal)ratio));
    }
}
