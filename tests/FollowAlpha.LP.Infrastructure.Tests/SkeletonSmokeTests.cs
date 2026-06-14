using FluentAssertions;
using FollowAlpha.LP.Infrastructure;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests;

/// <summary>Phase 0 placeholder: proves the test harness wires up. Real adapter tests land in Phase 2.</summary>
public class SkeletonSmokeTests
{
    [Fact]
    public void Infrastructure_assembly_is_reachable()
    {
        typeof(AssemblyReference).Assembly.GetName().Name.Should().Be("FollowAlpha.LP.Infrastructure");
    }
}
