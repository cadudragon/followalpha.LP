using FluentAssertions;
using FollowAlpha.LP.Domain;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests;

/// <summary>Phase 0 placeholder: proves the test harness wires up. Real domain tests land in Phase 1.</summary>
public class SkeletonSmokeTests
{
    [Fact]
    public void Domain_assembly_is_reachable()
    {
        typeof(AssemblyReference).Assembly.GetName().Name.Should().Be("FollowAlpha.LP.Domain");
    }
}
