using FluentAssertions;
using FollowAlpha.LP.Application;
using Xunit;

namespace FollowAlpha.LP.Application.Tests;

/// <summary>Phase 0 placeholder: proves the test harness wires up. Real use-case tests land in Phase 2+.</summary>
public class SkeletonSmokeTests
{
    [Fact]
    public void Application_assembly_is_reachable()
    {
        typeof(AssemblyReference).Assembly.GetName().Name.Should().Be("FollowAlpha.LP.Application");
    }
}
