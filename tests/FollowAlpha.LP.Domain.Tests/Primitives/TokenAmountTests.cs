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

    [Fact]
    public void Constructor_rejects_negative_raw()
    {
        var act = () => new TokenAmount(BigInteger.MinusOne, 6);
        act.Should().Throw<ArgumentOutOfRangeException>();
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
        new TokenAmount(new BigInteger(1_500_000), 6).ToDecimal().Should().Be(1.5m);
    }

    [Fact]
    public void To_decimal_handles_raw_values_larger_than_the_decimal_range()
    {
        var raw = BigInteger.Pow(10, 30);
        new TokenAmount(raw, 18).ToDecimal().Should().Be(1_000_000_000_000m);
    }

    [Fact]
    public void From_decimal_exact_round_trips()
    {
        TokenAmount.FromDecimalExact(1.5m, 6).Raw.Should().Be(new BigInteger(1_500_000));
        TokenAmount.FromDecimalExact(1.5m, 6).ToDecimal().Should().Be(1.5m);
    }

    [Fact]
    public void From_decimal_exact_rejects_values_finer_than_the_decimals()
    {
        // 1.5 cannot be represented in 0-decimal base units.
        var act = () => TokenAmount.FromDecimalExact(1.5m, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void From_decimal_floor_truncates_the_sub_unit_remainder()
    {
        TokenAmount.FromDecimalFloor(1.9m, 0).Raw.Should().Be(new BigInteger(1));
    }

    [Fact]
    public void From_decimal_rounded_uses_the_explicit_mode()
    {
        TokenAmount.FromDecimalRounded(1.5m, 0, MidpointRounding.ToEven).Raw.Should().Be(new BigInteger(2));
        TokenAmount.FromDecimalRounded(2.5m, 0, MidpointRounding.ToEven).Raw.Should().Be(new BigInteger(2));
        TokenAmount.FromDecimalRounded(2.5m, 0, MidpointRounding.AwayFromZero).Raw.Should().Be(new BigInteger(3));
    }

    [Fact]
    public void From_decimal_rejects_negative_human_values()
    {
        var exact = () => TokenAmount.FromDecimalExact(-1m, 6);
        var floor = () => TokenAmount.FromDecimalFloor(-1m, 6);
        var rounded = () => TokenAmount.FromDecimalRounded(-1m, 6, MidpointRounding.ToEven);
        exact.Should().Throw<ArgumentOutOfRangeException>();
        floor.Should().Throw<ArgumentOutOfRangeException>();
        rounded.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void From_decimal_rejects_out_of_range_decimals()
    {
        var act = () => TokenAmount.FromDecimalFloor(1m, 29);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
