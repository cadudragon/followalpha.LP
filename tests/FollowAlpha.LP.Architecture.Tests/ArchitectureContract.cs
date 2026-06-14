using System.Reflection;

namespace FollowAlpha.LP.Architecture.Tests;

/// <summary>
/// Single source of truth for assembly names and anchors used by the architecture suite.
/// </summary>
internal static class ArchitectureContract
{
    public const string Domain = "FollowAlpha.LP.Domain";
    public const string Application = "FollowAlpha.LP.Application";
    public const string Infrastructure = "FollowAlpha.LP.Infrastructure";
    public const string Api = "FollowAlpha.LP.Api";
    public const string Collector = "FollowAlpha.LP.Collector";
    public const string Cli = "FollowAlpha.LP.Cli";

    public static Assembly DomainAssembly => typeof(LP.Domain.AssemblyReference).Assembly;
    public static Assembly ApplicationAssembly => typeof(LP.Application.AssemblyReference).Assembly;
    public static Assembly InfrastructureAssembly => typeof(LP.Infrastructure.AssemblyReference).Assembly;
}
