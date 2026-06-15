using System.Globalization;
using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceMathTests
{
    private static readonly BigInteger Q96Literal = BigInteger.Parse("79228162514264337593543950336", CultureInfo.InvariantCulture);

    [Fact]
    public void Q96_matches_the_known_constant()
    {
        PriceMath.Q96.Should().Be(Q96Literal);
    }

    // ---- tick -> decimal pool price (analytics view) ----

    [Fact]
    public void Tick_zero_is_price_one()
    {
        PriceMath.TickToPoolPrice(0).Should().Be(1m);
    }

    [Fact]
    public void Tick_one_is_the_base()
    {
        PriceMath.TickToPoolPrice(1).Should().Be(1.0001m);
    }

    [Fact]
    public void Negative_tick_is_the_reciprocal_of_the_base()
    {
        PriceMath.TickToPoolPrice(-1).Should().Be(1m / 1.0001m);
    }

    [Theory]
    [InlineData(-100000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100000)]
    public void Tick_to_pool_price_is_strictly_monotonic(int tick)
    {
        PriceMath.TickToPoolPrice(tick + 1).Should().BeGreaterThan(PriceMath.TickToPoolPrice(tick));
    }

    [Fact]
    public void Tick_to_pool_price_throws_outside_the_valid_tick_range()
    {
        var act = () => PriceMath.TickToPoolPrice(PriceMath.MaxTick + 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tick_to_pool_price_throws_a_domain_exception_outside_the_decimal_window()
    {
        // Full Uniswap range is valid for the raw types; the analytics decimal view is what is bounded.
        var high = () => PriceMath.TickToPoolPrice(PriceMath.MaxTick);
        var low = () => PriceMath.TickToPoolPrice(PriceMath.MinTick);
        high.Should().Throw<PriceOutsideDecimalRangeException>();
        low.Should().Throw<PriceOutsideDecimalRangeException>();
    }

    // ---- decimal pool price -> tick (floor invariant + verified guard) ----

    [Theory]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("0.5")]
    [InlineData("500000000")]
    [InlineData("0.0000000002")]
    public void Pool_price_to_tick_satisfies_the_floor_invariant(string priceText)
    {
        var price = decimal.Parse(priceText, CultureInfo.InvariantCulture);

        var tick = PriceMath.PoolPriceToTick(price);

        PriceMath.TickToPoolPrice(tick).Should().BeLessThanOrEqualTo(price);
        PriceMath.TickToPoolPrice(tick + 1).Should().BeGreaterThan(price);
    }

    [Fact]
    public void Dense_sweep_round_trips_every_tick_through_its_price()
    {
        // Thousands of real boundaries: a raw floor(log/log) would mis-round at some of these; the
        // verified ±1 guard must hold the exact invariant everywhere.
        for (var t = -5000; t <= 5000; t++)
        {
            PriceMath.PoolPriceToTick(PriceMath.TickToPoolPrice(t)).Should().Be(t);
        }

        // Wider, sampled, inside the analytics window where decimal keeps enough significant digits
        // (±300k comfortably covers real pools — e.g. USDC/WETH sits near ±200k). Far below that the
        // raw price magnitude (~1e-27) collapses decimal precision; that tail is analytics-only.
        for (var t = -300000; t <= 300000; t += 997)
        {
            PriceMath.PoolPriceToTick(PriceMath.TickToPoolPrice(t)).Should().Be(t);
        }
    }

    [Fact]
    public void Price_just_below_a_tick_boundary_floors_to_the_tick_below()
    {
        const int t = 100;
        var boundary = PriceMath.TickToPoolPrice(t);
        var justBelow = boundary - boundary * 0.00001m;

        PriceMath.PoolPriceToTick(boundary).Should().Be(t);
        PriceMath.PoolPriceToTick(justBelow).Should().Be(t - 1);
    }

    [Fact]
    public void Pool_price_to_tick_rejects_non_positive_prices()
    {
        var zero = () => PriceMath.PoolPriceToTick(0m);
        var neg = () => PriceMath.PoolPriceToTick(-1m);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        neg.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- sqrtPriceX96 (analytics decimal view) ----

    [Fact]
    public void Q96_maps_back_to_price_one()
    {
        PriceMath.SqrtPriceX96ToPoolPrice(Q96Literal).Should().Be(1m);
    }

    [Theory]
    [InlineData(-50000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50000)]
    public void Sqrt_ratio_decimal_view_matches_the_geometric_price_within_tolerance(int tick)
    {
        var expected = PriceMath.TickToPoolPrice(tick);
        var fromSqrt = PriceMath.SqrtPriceX96ToPoolPrice(TickMath.GetSqrtRatioAtTick(tick));

        fromSqrt.Should().BeApproximately(expected, expected * PriceMath.SqrtRoundTripRelativeTolerance);
    }

    [Fact]
    public void Sqrt_price_decimal_view_rejects_non_positive_values()
    {
        var act = () => PriceMath.SqrtPriceX96ToPoolPrice(BigInteger.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- decimal scaling ----

    [Fact]
    public void Canonical_human_to_raw_applies_the_decimal_factor()
    {
        // USDC(6)/WETH(18): canonical human 0.0005 -> raw 0.0005 * 10^12 = 5e8.
        var decimals = new TokenDecimals(6, 18);
        PriceMath.CanonicalHumanToRawPrice(0.0005m, decimals).Should().Be(500_000_000m);
    }

    [Fact]
    public void Raw_to_canonical_human_is_the_inverse_scaling()
    {
        var decimals = new TokenDecimals(6, 18);
        PriceMath.RawPriceToCanonicalHuman(500_000_000m, decimals).Should().Be(0.0005m);
    }

    [Fact]
    public void Equal_decimals_make_scaling_a_no_op()
    {
        var decimals = new TokenDecimals(18, 18);
        PriceMath.CanonicalHumanToRawPrice(2000m, decimals).Should().Be(2000m);
        PriceMath.RawPriceToCanonicalHuman(2000m, decimals).Should().Be(2000m);
    }

    [Fact]
    public void Scaling_round_trips()
    {
        var decimals = new TokenDecimals(8, 6); // dec1 < dec0
        var raw = PriceMath.CanonicalHumanToRawPrice(1234.5m, decimals);
        PriceMath.RawPriceToCanonicalHuman(raw, decimals).Should().Be(1234.5m);
    }
}
