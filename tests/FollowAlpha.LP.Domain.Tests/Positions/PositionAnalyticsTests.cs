using System.Numerics;
using FollowAlpha.LP.Domain.Positions;
using FollowAlpha.LP.Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Positions;

public class PositionAnalyticsTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RangePosition Position() => new(
        new Tick(-1000),
        new Tick(1000),
        new Liquidity(new BigInteger(1_000_000)),
        Opened,
        FeeTier.Medium,
        new TokenDecimals(18, 18));

    [Fact]
    public void Impermanent_loss_is_benchmark_minus_position()
    {
        PositionAnalytics.ImpermanentLoss(positionValue: 9000m, benchmarkValue: 9500m).Should().Be(500m);
        PositionAnalytics.ImpermanentLoss(positionValue: 9500m, benchmarkValue: 9000m).Should().Be(-500m);
    }

    [Fact]
    public void Net_value_credits_fees_and_charges_exit_cost()
    {
        PositionAnalytics.NetValueAfterCosts(positionValue: 10_000m, feesEarned: 200m, exitCost: 50m)
            .Should().Be(10_150m);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Net_value_rejects_negative_fees_or_exit_cost(decimal fees, decimal exit)
    {
        var act = () => PositionAnalytics.NetValueAfterCosts(10_000m, fees, exit);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Value_along_path_returns_one_valuation_per_price()
    {
        var path = new[] { new PoolPrice(0.5m), new PoolPrice(1.0m), new PoolPrice(2.0m) };

        var series = PositionAnalytics.ValueAlongPath(Position(), path);

        series.Should().HaveCount(3);
        series[0].Value.Should().BeLessThan(series[1].Value);
        series[1].Value.Should().BeLessThan(series[2].Value);
    }

    [Fact]
    public void Harvest_position_can_be_judged_against_hodl_and_fifty_fifty()
    {
        // End-to-end: value a harvest position, build its two benchmarks from entry, measure IL.
        var position = Position();
        var entryPrice = new PoolPrice(1.0m);
        var entry = position.ValueAt(entryPrice);

        var hodl = HodlBenchmark.FromEntry(entry);
        var fiftyFifty = FiftyFiftyBenchmark.FromEntry(entry, entryPrice.RawToken1PerToken0);

        var later = new PoolPrice(1.05m);
        var positionValue = position.ValueAt(later).Value;

        // At entry both benchmarks equal the entry value; IL is zero there.
        PositionAnalytics.ImpermanentLoss(entry.Value, hodl.ValueAt(entryPrice.RawToken1PerToken0)).Should().Be(0m);
        PositionAnalytics.ImpermanentLoss(entry.Value, fiftyFifty.ValueAt(entryPrice.RawToken1PerToken0)).Should().Be(0m);

        // Away from entry an in-range LP underperforms holding (classic IL >= 0).
        PositionAnalytics.ImpermanentLoss(positionValue, hodl.ValueAt(1.05m)).Should().BeGreaterThan(0m);
    }
}
