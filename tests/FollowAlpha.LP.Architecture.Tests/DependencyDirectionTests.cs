using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Enforces the dependency law (AGENTS.md hard rule 1 / ARCHITECTURE.md §3):
/// Domain ◄ Application ◄ Infrastructure; hosts sit on top.
/// </summary>
public class DependencyDirectionTests
{
    [Fact]
    public void Domain_must_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(ArchitectureContract.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                ArchitectureContract.Application,
                ArchitectureContract.Infrastructure,
                ArchitectureContract.Api,
                ArchitectureContract.Collector,
                ArchitectureContract.Cli)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result, "Domain must reference nothing"));
    }

    [Fact]
    public void Application_must_not_depend_on_infrastructure_or_hosts()
    {
        var result = Types.InAssembly(ArchitectureContract.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                ArchitectureContract.Infrastructure,
                ArchitectureContract.Api,
                ArchitectureContract.Collector,
                ArchitectureContract.Cli)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result, "Application may reference Domain only"));
    }

    [Fact]
    public void Infrastructure_must_not_depend_on_hosts()
    {
        var result = Types.InAssembly(ArchitectureContract.InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                ArchitectureContract.Api,
                ArchitectureContract.Collector,
                ArchitectureContract.Cli)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result, "Infrastructure may reference Application + Domain only"));
    }

    private static string FailureMessage(TestResult result, string rule)
    {
        var offenders = result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
        return $"{rule}. Offending types: {offenders}";
    }
}
