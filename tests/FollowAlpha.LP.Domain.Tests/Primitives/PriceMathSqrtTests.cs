using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceMathSqrtTests
{
    [Fact]
    public void Sqrt_of_zero_is_zero()
    {
        PriceMath.Sqrt(0m).Should().Be(0m);
    }

    [Fact]
    public void Sqrt_rejects_negative_input()
    {
        var act = () => PriceMath.Sqrt(-1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("4", "2")]
    [InlineData("0.25", "0.5")]
    [InlineData("2", "1.4142135623730950488016887242")]
    [InlineData("2000", "44.721359549995793928183473374")]
    public void Sqrt_computes_known_roots(string input, string expected)
    {
        var x = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        var exp = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        PriceMath.Sqrt(x).Should().BeApproximately(exp, 1e-20m);
    }

    [Fact]
    public void Sqrt_squared_recovers_the_input()
    {
        var root = PriceMath.Sqrt(1234.5678m);
        (root * root).Should().BeApproximately(1234.5678m, 1e-20m);
    }
}
