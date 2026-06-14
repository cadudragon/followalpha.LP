using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class TokenAmountTests
{
    [Fact]
    public void Constructor_stores_raw_and_decimals()
    {
        var amount = new TokenAmount(new BigInteger(1_500_000), 6);
        amount.Raw.Should().Be(new BigInteger(1_500_000));
        amount.Decimals.Should().Be(6);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public void Constructor_rejects_out_of_range_decimals(int decimals)
    {
        var act = () => new TokenAmount(BigInteger.One, decimals);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void To_decimal_scales_by_decimals()
    {
        // 1_500_000 base units at 6 decimals = 1.5 (USDC-style).
        new TokenAmount(new BigInteger(1_500_000), 6).ToDecimal().Should().Be(1.5m);
    }

    [Fact]
    public void To_decimal_handles_raw_values_larger_than_the_decimal_range()
    {
        // 10^30 wei at 18 decimals = 10^12 — the integer part fits decimal once scaled down,
        // even though the raw 10^30 does not.
        var raw = BigInteger.Pow(10, 30);
        new TokenAmount(raw, 18).ToDecimal().Should().Be(1_000_000_000_000m);
    }

    [Theory]
    [InlineData("1.5", 6)]
    [InlineData("0.000001", 6)]
    [InlineData("1234.567890123456789", 18)]
    public void From_decimal_round_trips_through_to_decimal(string humanText, int decimals)
    {
        var human = decimal.Parse(humanText, System.Globalization.CultureInfo.InvariantCulture);

        TokenAmount.FromDecimal(human, decimals).ToDecimal().Should().Be(human);
    }

    [Fact]
    public void From_decimal_rounds_to_the_nearest_base_unit()
    {
        // 1.5 base units worth (at 0 decimals) rounds to even -> 2.
        TokenAmount.FromDecimal(1.5m, 0).Raw.Should().Be(new BigInteger(2));
        TokenAmount.FromDecimal(2.5m, 0).Raw.Should().Be(new BigInteger(2));
    }

    [Fact]
    public void From_decimal_rejects_out_of_range_decimals()
    {
        var act = () => TokenAmount.FromDecimal(1m, 29);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
