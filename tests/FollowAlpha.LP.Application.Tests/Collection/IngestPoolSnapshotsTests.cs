using FollowAlpha.LP.Application.Collection;
using FollowAlpha.LP.Application.Pools;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Application.Tests.Collection;

public class IngestPoolSnapshotsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly PoolToSnapshot Pool = new("arbitrum:0xpool", "arbitrum", "0xpool");

    private static (IngestPoolSnapshots UseCase, FakePoolDataSource Source, InMemorySnapshotStore Store, FixedClock Clock) Build()
    {
        var source = new FakePoolDataSource();
        source.DayVolumes.Add(new PoolDayVolume(T0, 1234m));
        source.Ticks.Add(new TickLiquidity(-100, "5", "5"));
        source.Ticks.Add(new TickLiquidity(100, "-5", "5"));
        var store = new InMemorySnapshotStore();
        var clock = new FixedClock(T0);
        return (new IngestPoolSnapshots(source, store, clock), source, store, clock);
    }

    [Fact]
    public async Task Snapshots_pool_state_and_full_tick_distribution()
    {
        var (useCase, _, store, _) = Build();

        var outcomes = await useCase.RunAsync([Pool]);

        outcomes.Should().ContainSingle();
        outcomes[0].PoolSnapshotInserted.Should().BeTrue();
        outcomes[0].TickRowsInserted.Should().Be(2);
        store.PoolSnapshots.Should().ContainSingle();
        store.PoolSnapshots[0].DayVolumeUsd.Should().Be(1234m);
        store.PoolSnapshots[0].AsOfUtc.Should().Be(T0);
        store.TickSnapshots.Should().HaveCount(2);
    }

    [Fact]
    public async Task Rerun_with_the_same_clock_inserts_nothing_new()
    {
        var (useCase, _, store, _) = Build();

        await useCase.RunAsync([Pool]);
        var second = await useCase.RunAsync([Pool]);

        second[0].PoolSnapshotInserted.Should().BeFalse();
        second[0].TickRowsInserted.Should().Be(0);
        store.PoolSnapshots.Should().ContainSingle();
        store.TickSnapshots.Should().HaveCount(2);
    }

    [Fact]
    public async Task One_failing_pool_is_recorded_and_does_not_abort_the_batch()
    {
        var (useCase, source, store, _) = Build();
        source.FailFor = address => address == "0xbad" ? new InvalidOperationException("boom") : null;

        var outcomes = await useCase.RunAsync([new PoolToSnapshot("arbitrum:0xbad", "arbitrum", "0xbad"), Pool]);

        outcomes.Should().HaveCount(2);
        outcomes[0].Error.Should().Be("boom");
        outcomes[1].Error.Should().BeNull();
        store.PoolSnapshots.Should().ContainSingle(s => s.PoolId == "arbitrum:0xpool");
    }
}
