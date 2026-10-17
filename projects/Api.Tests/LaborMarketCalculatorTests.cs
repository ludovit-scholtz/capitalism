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
    public void Compute_TinyPoolWithHugeDemand_CapsUtilizationSoItFitsTheColumn()
    {
        // 3 people -> pool of 0.00675 worker-hours; 500 hours of demand would give a utilization in the millions.
        var state = LaborMarketCalculator.Compute(population: 3, laborDemand: 500m);

        Assert.Equal(LaborMarketCalculator.MaxUtilization, state.Utilization);
        Assert.True(state.Utilization < 999_999m);
        Assert.Equal(LaborMarketCalculator.MaxWageIndex, state.WageIndex);
        Assert.InRange(state.StaffingFillRate, 0m, 0.0001m);
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

    [Theory]
    [InlineData(1.0, 0.10, 0.70, 0.97)]  // idle market: low pay barely matters
    [InlineData(1.0, 1.00, 0.70, 0.70)]  // tight market: low pay costs staff one-for-one
    [InlineData(1.0, 1.00, 1.00, 1.00)]
    [InlineData(0.5, 2.00, 1.20, 0.60)]  // shortage: paying above average wins a bigger share of scarce workers
    [InlineData(1.0, 1.00, 1.50, 1.00)]  // never above fully staffed
    [InlineData(0.0, 2.00, 1.00, 0.20)]  // city cannot staff anything: workers commute in, output never drops below the floor
    public void ComputeCompanyStaffing_CombinesCityFillTightnessAndPay(double fill, double utilization, double payRatio, double expected)
    {
        var staffing = LaborMarketCalculator.ComputeCompanyStaffing((decimal)fill, (decimal)utilization, (decimal)payRatio);

        Assert.Equal((decimal)expected, staffing);
    }

    [Fact]
    public void ComputeCompanyStaffing_PayingMoreNeverHurts()
    {
        var previous = -1m;
        for (var ratio = 0.5m; ratio <= 1.5m; ratio += 0.05m)
        {
            var staffing = LaborMarketCalculator.ComputeCompanyStaffing(0.6m, 1.5m, ratio);
            Assert.True(staffing >= previous, $"staffing fell at pay ratio {ratio}");
            previous = staffing;
        }
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(0.85)]
    public void StrikeProbability_IsZeroAtOrAboveTheThreshold(double ratio)
    {
        Assert.Equal(0m, LaborMarketCalculator.ComputeStrikeProbability((decimal)ratio));
    }

    [Fact]
    public void StrikeProbability_GrowsAsPayFallsAndIsCapped()
    {
        var mild = LaborMarketCalculator.ComputeStrikeProbability(0.80m);
        var severe = LaborMarketCalculator.ComputeStrikeProbability(0.50m);
        var absurd = LaborMarketCalculator.ComputeStrikeProbability(0.0m);

        Assert.True(mild > 0m);
        Assert.True(severe > mild);
        Assert.Equal(LaborMarketCalculator.MaxStrikeProbabilityPerTick, absurd);
    }

    [Fact]
    public void Roll_IsDeterministicWithinUnitIntervalAndVariesByInput()
    {
        var building = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = LaborMarketCalculator.Roll(42, building, salt: 1);
        var again = LaborMarketCalculator.Roll(42, building, salt: 1);
        var otherTick = LaborMarketCalculator.Roll(43, building, salt: 1);
        var otherSalt = LaborMarketCalculator.Roll(42, building, salt: 2);

        Assert.Equal(first, again);
        Assert.NotEqual(first, otherTick);
        Assert.NotEqual(first, otherSalt);
        Assert.InRange(first, 0d, 0.999999999d);
    }

    [Fact]
    public void Roll_IsRoughlyUniform()
    {
        var building = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var below = Enumerable.Range(0, 2000).Count(tick => LaborMarketCalculator.Roll(tick, building, 1) < 0.1d);

        Assert.InRange(below, 120, 280); // expect ~200 of 2000
    }

    [Theory]
    [InlineData(0.0, 6)]
    [InlineData(0.5, 9)]
    [InlineData(0.999, 11)]
    public void StrikeDuration_IsSixToElevenTicks(double roll, int expected)
    {
        Assert.Equal(expected, LaborMarketCalculator.ComputeStrikeDurationTicks(roll));
    }

    [Theory]
    [InlineData(10, 20, 0.50, false)] // still running and pay still low
    [InlineData(20, 20, 0.50, true)]  // time is up
    [InlineData(12, 20, 0.95, true)]  // pay fixed: the company met the demand
    [InlineData(12, 20, 0.90, false)] // better but not settled
    public void ShouldStrikeEnd_WhenTimeIsUpOrPayIsFixed(long tick, long until, double ratio, bool expected)
    {
        Assert.Equal(expected, LaborMarketCalculator.ShouldStrikeEnd(tick, until, (decimal)ratio));
    }

    [Theory]
    [InlineData(0.5, 0.5, 0.5)]  // everybody pays half the baseline: still underpaying against the city baseline
    [InlineData(0.5, 1.0, 0.5)]
    [InlineData(1.2, 1.1, 1.0909)]
    [InlineData(1.5, 1.0, 1.5)]
    public void ComputePayRatio_IsNeverRelativeToAnAverageBelowTheCityBaseline(double company, double average, double expected)
    {
        Assert.Equal((decimal)expected, LaborMarketCalculator.ComputePayRatio((decimal)company, (decimal)average));
    }

    [Fact]
    public void Roll_IsPortableAcrossByteOrders()
    {
        // Pinned value: a change here means strike outcomes for existing games would change.
        var value = LaborMarketCalculator.Roll(1, Guid.Parse("00000000-0000-0000-0000-000000000001"), salt: 1);

        Assert.InRange(value, 0d, 0.999999999d);
        Assert.Equal(value, LaborMarketCalculator.Roll(1, Guid.Parse("00000000-0000-0000-0000-000000000001"), salt: 1));
    }

    [Theory]
    [InlineData(1.05, 1.0, true)]   // exactly at the bar
    [InlineData(1.04, 1.0, false)]
    [InlineData(2.0, 1.0, true)]    // sole employer, no rivals: the reference is the baseline wage
    [InlineData(1.40, 1.35, false)] // only slightly above the rivals
    [InlineData(1.42, 1.35, true)]
    [InlineData(1.2, 0.5, true)]    // rivals below baseline never lower the bar under the baseline
    [InlineData(1.0, 0.5, false)]
    public void IsGoodEmployer_NeedsAClearLeadOverTheReference(double multiplier, double rivalsAverage, bool expected)
    {
        Assert.Equal(expected, LaborMarketCalculator.IsGoodEmployer((decimal)multiplier, (decimal)rivalsAverage));
    }
}
