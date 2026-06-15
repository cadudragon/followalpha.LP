using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceMathLnTests
{
    [Fact]
    public void Ln_of_one_is_zero()
    {
        PriceMath.Ln(1m).Should().Be(0m);
    }

    [Theory]
    [InlineData("2", "0.6931471805599453094172321215")]
    [InlineData("4", "1.3862943611198906188344642430")]
    [InlineData("0.5", "-0.6931471805599453094172321215")]
    [InlineData("10", "2.3025850929940456840179914547")]
    public void Ln_matches_known_values(string input, string expected)
    {
        var x = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        var exp = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        PriceMath.Ln(x).Should().BeApproximately(exp, 1e-20m);
    }

    [Fact]
    public void Ln_rejects_non_positive_input()
    {
        var zero = () => PriceMath.Ln(0m);
        var neg = () => PriceMath.Ln(-1m);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        neg.Should().Throw<ArgumentOutOfRangeException>();
    }
}
