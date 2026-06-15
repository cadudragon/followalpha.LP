using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Asserts the Domain is BCL-only at the project level: its csproj declares no <c>PackageReference</c>.
/// The dependency/namespace rules in <see cref="DomainPurityTests"/> only catch banned namespaces; a
/// third-party package that happens to avoid them would slip through. This closes that gap by reading
/// the project file directly (AGENTS.md hard rule 1; ARCHITECTURE.md §4.1 hardening, 2026-06-15).
/// </summary>
public class DomainBclOnlyTests
{
    [Fact]
    public void Domain_project_declares_no_package_references()
    {
        var csprojPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "FollowAlpha.LP.Domain",
            "FollowAlpha.LP.Domain.csproj");

        File.Exists(csprojPath).Should().BeTrue($"Domain csproj should exist at {csprojPath}");

        var project = XDocument.Load(csprojPath);

        var packageReferences = project
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "(unnamed)")
            .ToList();

        packageReferences.Should().BeEmpty(
            "the Domain must reference no third-party packages (BCL-only). Found: {0}",
            string.Join(", ", packageReferences));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FollowAlpha.LP.slnx")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root (containing FollowAlpha.LP.slnx) should be locatable from the test output dir");
        return dir!.FullName;
    }
}
