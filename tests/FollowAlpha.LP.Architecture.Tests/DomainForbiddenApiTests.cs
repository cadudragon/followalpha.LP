using FluentAssertions;
using Xunit;
using static FollowAlpha.LP.Architecture.Tests.ForbiddenMemberScanner;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Enforces the ambient-clock ban (AGENTS.md hard rule 2): the Domain must take time via an
/// <c>IClock</c> parameter, never read it from the runtime. These are member calls, so they are
/// caught by scanning the compiled IL rather than by NetArchTest's type-dependency model.
/// </summary>
public class DomainForbiddenApiTests
{
    private static readonly ForbiddenMember[] AmbientClockApis =
    [
        new("System.DateTime", "get_Now"),
        new("System.DateTime", "get_UtcNow"),
        new("System.DateTime", "get_Today"),
        new("System.DateTimeOffset", "get_Now"),
        new("System.DateTimeOffset", "get_UtcNow"),
    ];

    [Fact]
    public void Domain_must_not_read_the_ambient_clock()
    {
        var assemblyPath = ArchitectureContract.DomainAssembly.Location;

        var violations = Scan(assemblyPath, AmbientClockApis);

        violations.Should().BeEmpty(
            "Domain must receive time via IClock, never call DateTime.Now/UtcNow/Today. Found: {0}",
            string.Join(", ", violations));
    }
}
