using System.Globalization;
using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class LiquidityTests
{
    [Fact]
    public void Constructor_stores_the_value()
    {
        var liquidity = new Liquidity(BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture));
        liquidity.Value.Should().Be(BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Constructor_rejects_negative_liquidity()
    {
        var act = () => new Liquidity(BigInteger.MinusOne);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Zero_is_zero()
    {
        Liquidity.Zero.Value.Should().Be(BigInteger.Zero);
    }

    [Fact]
    public void Records_with_the_same_value_are_equal()
    {
        new Liquidity(new BigInteger(42)).Should().Be(new Liquidity(new BigInteger(42)));
    }
}
