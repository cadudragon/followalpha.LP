using System.Globalization;
using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceMathTests
{
    // The canonical Q96 constant (2^96), the fixed-point scale of sqrtPriceX96.
    private static readonly BigInteger Q96Literal = BigInteger.Parse("79228162514264337593543950336", CultureInfo.InvariantCulture);

    [Fact]
    public void Q96_matches_the_known_constant()
    {
        PriceMath.Q96.Should().Be(Q96Literal);
    }

    // ---- tick -> price (reference points) ----

    [Fact]
    public void Tick_zero_is_price_one()
    {
        PriceMath.TickToPrice(0).Should().Be(1m);
    }

    [Fact]
    public void Tick_one_is_the_base()
    {
        PriceMath.TickToPrice(1).Should().Be(1.0001m);
    }

    [Fact]
    public void Negative_tick_is_the_reciprocal_of_the_base()
    {
        PriceMath.TickToPrice(-1).Should().Be(1m / 1.0001m);
    }

    [Theory]
    [InlineData(-100000)]
    [InlineData(-5000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5000)]
    [InlineData(100000)]
    public void Tick_to_price_is_strictly_monotonic(int tick)
    {
        PriceMath.TickToPrice(tick + 1).Should().BeGreaterThan(PriceMath.TickToPrice(tick));
    }

    [Fact]
    public void Tick_to_price_throws_outside_the_valid_range()
    {
        var act = () => PriceMath.TickToPrice(PriceMath.MaxTick + 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tick_to_price_overflows_at_the_extreme_tail_of_the_decimal_window()
    {
        // Documents the analytics-grade decimal boundary: the extreme Uniswap ticks have prices outside
        // the decimal magnitude window and throw by design (no real asset reaches them).
        var act = () => PriceMath.TickToPrice(PriceMath.MaxTick);
        act.Should().Throw<OverflowException>();
    }

    // ---- price -> tick (floor invariant + verified guard) ----

    [Theory]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("0.5")]
    [InlineData("2000")]
    [InlineData("1234.5678")]
    [InlineData("0.0009")]
    public void Price_to_tick_satisfies_the_floor_invariant(string priceText)
    {
        var price = decimal.Parse(priceText, System.Globalization.CultureInfo.InvariantCulture);

        var tick = PriceMath.PriceToTick(price);

        PriceMath.TickToPrice(tick).Should().BeLessThanOrEqualTo(price);
        PriceMath.TickToPrice(tick + 1).Should().BeGreaterThan(price);
    }

    [Fact]
    public void Price_exactly_on_a_tick_maps_to_that_tick()
    {
        foreach (var t in new[] { -5000, -1, 0, 1, 100, 5000 })
        {
            PriceMath.PriceToTick(PriceMath.TickToPrice(t)).Should().Be(t);
        }
    }

    [Fact]
    public void Price_just_below_a_tick_boundary_floors_to_the_tick_below()
    {
        const int t = 100;
        var boundary = PriceMath.TickToPrice(t);

        // A hair below the boundary, but still above tick t-1: the verified ±1 guard must return t-1,
        // never the raw floor(log/log) which floating error can place on the wrong side.
        var justBelow = boundary - boundary * 0.00001m;

        PriceMath.PriceToTick(boundary).Should().Be(t);
        PriceMath.PriceToTick(justBelow).Should().Be(t - 1);
    }

    [Fact]
    public void Price_to_tick_rejects_non_positive_prices()
    {
        var actZero = () => PriceMath.PriceToTick(0m);
        var actNeg = () => PriceMath.PriceToTick(-1m);
        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- sqrt-price (reference points + round trips) ----

    [Fact]
    public void Price_one_maps_to_Q96()
    {
        PriceMath.PriceToSqrtPriceX96(1m).Should().Be(Q96Literal);
    }

    [Fact]
    public void Tick_zero_maps_to_Q96()
    {
        PriceMath.TickToSqrtPriceX96(0).Should().Be(Q96Literal);
    }

    [Fact]
    public void Q96_maps_back_to_price_one()
    {
        PriceMath.SqrtPriceX96ToPrice(Q96Literal).Should().Be(1m);
    }

    [Fact]
    public void Q96_maps_back_to_tick_zero()
    {
        PriceMath.SqrtPriceX96ToTick(Q96Literal).Should().Be(0);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0.5")]
    [InlineData("2000")]
    [InlineData("0.0025")]
    public void Price_round_trips_through_sqrt_price_within_tolerance(string priceText)
    {
        var price = decimal.Parse(priceText, System.Globalization.CultureInfo.InvariantCulture);

        var roundTripped = PriceMath.SqrtPriceX96ToPrice(PriceMath.PriceToSqrtPriceX96(price));

        roundTripped.Should().BeApproximately(price, price * PriceMath.SqrtRoundTripRelativeTolerance);
    }

    [Theory]
    [InlineData(-50000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50000)]
    public void Tick_round_trips_to_sqrt_price_and_back_to_a_price_within_tolerance(int tick)
    {
        var expected = PriceMath.TickToPrice(tick);

        var roundTripped = PriceMath.SqrtPriceX96ToPrice(PriceMath.TickToSqrtPriceX96(tick));

        roundTripped.Should().BeApproximately(expected, expected * PriceMath.SqrtRoundTripRelativeTolerance);
    }

    [Fact]
    public void Sqrt_price_conversions_reject_non_positive_values()
    {
        var actPrice = () => PriceMath.PriceToSqrtPriceX96(0m);
        var actSqrt = () => PriceMath.SqrtPriceX96ToPrice(BigInteger.Zero);
        actPrice.Should().Throw<ArgumentOutOfRangeException>();
        actSqrt.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- decimal sqrt ----

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("4", "2")]
    [InlineData("2", "1.4142135623730950488016887242")]
    public void Sqrt_computes_decimal_square_roots(string input, string expected)
    {
        var x = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        var exp = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        PriceMath.Sqrt(x).Should().BeApproximately(exp, 1e-20m);
    }

    [Fact]
    public void Sqrt_rejects_negative_input()
    {
        var act = () => PriceMath.Sqrt(-1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
