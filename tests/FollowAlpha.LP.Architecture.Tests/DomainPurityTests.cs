using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Enforces Domain purity (AGENTS.md hard rule 2): no I/O, no networking, no randomness, no
/// host/framework dependencies. The ambient-clock ban (DateTime.Now/UtcNow) is in
/// <see cref="DomainForbiddenApiTests"/> because it is a member call NetArchTest cannot express.
/// </summary>
public class DomainPurityTests
{
    [Fact]
    public void Domain_must_not_depend_on_io_or_networking()
    {
        var result = Types.InAssembly(ArchitectureContract.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "System.IO",
                "System.Net",
                "System.Net.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Offenders(result, "Domain must perform no I/O or networking"));
    }

    [Fact]
    public void Domain_must_not_depend_on_randomness()
    {
        var result = Types.InAssembly(ArchitectureContract.DomainAssembly)
            .Should()
            .NotHaveDependencyOn("System.Random")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Offenders(result, "Domain must be deterministic (no System.Random)"));
    }

    [Fact]
    public void Domain_must_not_depend_on_hosting_or_logging_frameworks()
    {
        var result = Types.InAssembly(ArchitectureContract.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.Extensions",
                "Microsoft.AspNetCore",
                "Serilog")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Offenders(result, "Domain must not touch hosting/logging frameworks"));
    }

    private static string Offenders(TestResult result, string rule)
    {
        var offenders = result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
        return $"{rule}. Offending types: {offenders}";
    }
}
