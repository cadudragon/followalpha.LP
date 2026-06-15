using FollowAlpha.LP.Domain.Positions;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Positions;

public class BenchmarkTests
{
    private const decimal Ln2 = 0.6931471805599453m;
    private const decimal Ln4 = 1.3862943611198906m;
    private const decimal Tol = 1e-6m;

    // ---- HODL ----

    [Fact]
    public void Hodl_values_the_initial_tokens_at_the_new_price()
    {
        var hodl = HodlBenchmark.FromEntry(new PositionValuation(AmountX: 2m, AmountY: 4000m, Value: 8000m));
        hodl.ValueAt(2000m).Should().Be(8000m);
        hodl.ValueAt(2500m).Should().Be(9000m);
    }

    // ---- 50/50 ----

    [Fact]
    public void Fifty_fifty_equals_entry_value_at_entry_price_and_scales_with_price()
    {
        var ff = new FiftyFiftyBenchmark(entryValue: 8000m, entryPrice: 2000m);
        ff.ValueAt(2000m).Should().Be(8000m);
        ff.ValueAt(2500m).Should().Be(9000m); // 4000 * (1 + 1.25)
        ff.ValueAt(1000m).Should().Be(6000m); // 4000 * (1 + 0.5)
    }

    [Fact]
    public void Fifty_fifty_rejects_non_positive_entry_price()
    {
        var act = () => new FiftyFiftyBenchmark(8000m, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- Limit order: ACCUMULATE (budget = token1, range [1,4]) ----

    [Fact]
    public void Accumulate_primary_full_fill_buys_at_the_logarithmic_mean()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
        var h = b.HoldingsAt(1m); // price at/below lower bound -> fully filled

        h.AmountX.Should().BeApproximately(Ln4, Tol);     // token0 = B/(b-a)*ln(b/a) = ln4
        h.AmountY.Should().BeApproximately(0m, Tol);       // budget fully spent
        (3m / h.AmountX).Should().BeApproximately(3m / Ln4, Tol); // avg fill = (b-a)/ln(b/a) = log mean
    }

    [Fact]
    public void Accumulate_secondary_full_fill_buys_at_the_arithmetic_mean()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformBaseByPrice);
        var h = b.HoldingsAt(1m);

        h.AmountX.Should().BeApproximately(1.2m, Tol);     // token0 = 2B/(a+b) = 6/5
        h.AmountY.Should().BeApproximately(0m, Tol);
        (3m / h.AmountX).Should().BeApproximately(2.5m, Tol); // avg fill = (a+b)/2
    }

    [Fact]
    public void Accumulate_primary_buys_more_token0_than_secondary_same_budget()
    {
        var primary = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice).HoldingsAt(1m);
        var secondary = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformBaseByPrice).HoldingsAt(1m);

        // Logarithmic mean < arithmetic mean -> primary fills cheaper -> more token0.
        primary.AmountX.Should().BeGreaterThan(secondary.AmountX);
    }

    [Fact]
    public void Accumulate_partial_fill_leaves_unspent_budget_as_token1()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
        var h = b.HoldingsAt(2m); // price inside range

        h.AmountY.Should().BeApproximately(1m, Tol);       // spent 3*(4-2)/3 = 2, remaining 1
        h.AmountX.Should().BeApproximately(Ln2, Tol);      // token0 = ln(4/2) = ln2
        h.Value.Should().BeApproximately(1m + Ln2 * 2m, Tol);
    }

    [Fact]
    public void Accumulate_no_fill_above_range_holds_the_whole_budget()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
        var h = b.HoldingsAt(4m); // price at/above upper bound

        h.AmountX.Should().BeApproximately(0m, Tol);
        h.AmountY.Should().BeApproximately(3m, Tol);
    }

    // ---- Limit order: DISTRIBUTE (budget = token0, range [1,4]) ----

    [Fact]
    public void Distribute_primary_full_fill_sells_at_the_logarithmic_mean()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Distribute, LimitLadder.UniformQuoteByPrice);
        var h = b.HoldingsAt(4m); // price at/above upper bound -> fully sold

        h.AmountX.Should().BeApproximately(0m, Tol);        // all token0 sold
        h.AmountY.Should().BeApproximately(9m / Ln4, Tol);  // proceeds = Q*(b-a)/ln(b/a)
    }

    [Fact]
    public void Distribute_secondary_full_fill_sells_at_the_arithmetic_mean()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Distribute, LimitLadder.UniformBaseByPrice);
        var h = b.HoldingsAt(4m);

        h.AmountX.Should().BeApproximately(0m, Tol);
        h.AmountY.Should().BeApproximately(7.5m, Tol);      // proceeds = q*(b^2-a^2)/2 = 15/2
    }

    [Fact]
    public void Distribute_partial_fill_keeps_unsold_token0()
    {
        var b = new LimitOrderBenchmark(3m, 1m, 4m, LadderSide.Distribute, LimitLadder.UniformQuoteByPrice);
        var h = b.HoldingsAt(2m); // price inside range

        h.AmountX.Should().BeApproximately(1.5m, Tol);      // sold r*ln2 = 1.5, remaining 1.5
        h.AmountY.Should().BeApproximately(3m / Ln4, Tol);  // proceeds r*(2-1)
        h.Value.Should().BeApproximately(3m / Ln4 + 1.5m * 2m, Tol);
    }

    [Fact]
    public void Constructor_rejects_bad_budget_and_range()
    {
        var badBudget = () => new LimitOrderBenchmark(0m, 1m, 4m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
        var badRange = () => new LimitOrderBenchmark(3m, 4m, 1m, LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
        badBudget.Should().Throw<ArgumentOutOfRangeException>();
        badRange.Should().Throw<ArgumentException>();
    }
}
