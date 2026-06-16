using FollowAlpha.LP.Application.Persistence;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.Persistence;

/// <summary>
/// Asserts append-only enforcement by interface shape (DATA-MODEL.md §4): the fact/decision/intent
/// stores expose insert/append + query only — no update/delete path exists to call. The rebuildable
/// <see cref="IPositionStore"/> projection is intentionally excluded (it upserts).
/// </summary>
public class AppendOnlyContractTests
{
    private static readonly string[] ForbiddenPrefixes =
        ["Update", "Delete", "Remove", "Upsert", "Replace", "Set", "Modify", "Patch", "Save", "Edit"];

    public static IEnumerable<object[]> AppendOnlyStores()
    {
        yield return [typeof(IPriceStore)];
        yield return [typeof(ISnapshotStore)];
        yield return [typeof(IPositionEventStore)];
        yield return [typeof(IIntentRecordStore)];
        yield return [typeof(IDecisionLog)];
    }

    [Theory]
    [MemberData(nameof(AppendOnlyStores))]
    public void Append_only_stores_expose_no_mutating_methods(Type storeInterface)
    {
        var offenders = storeInterface
            .GetMethods()
            .Select(m => m.Name)
            .Where(name => ForbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        offenders.Should().BeEmpty(
            "{0} is append-only and must expose only insert/append + query methods", storeInterface.Name);
    }

    [Fact]
    public void Position_projection_store_is_allowed_to_upsert()
    {
        // Sanity: the projection store is the one place an upsert is permitted (and is excluded above).
        typeof(IPositionStore).GetMethods().Select(m => m.Name).Should().Contain("UpsertAsync");
    }
}
