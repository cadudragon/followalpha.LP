using FollowAlpha.LP.Domain.Channels;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Channels;

public class ChannelSimulatorTests
{
    // Channel [100, 121] -> sqrt bounds 10 and 11, geometric-mean sell at 110. Deploy 110 of quote per
    // cycle (cap 100% of total 110): buys 1.1 token0 at the base, sells at 110 -> +11 per crossing.
    private static ChannelPolicy Policy(int maxReopens = 2, decimal floor = 80m) =>
        new(lowerPrice: 100m, upperPrice: 121m, capitalCapFraction: 1.0m, maxReopensWithoutFullCrossing: maxReopens, noReopenFloorPrice: floor);

    private static decimal[] Zeros(int n) => new decimal[n];

    [Fact]
    public void Full_crossing_realizes_the_geometric_mean_profit()
    {
        var sim = ChannelSimulator.Run(Policy(), [100m, 121m], Zeros(2), totalCapital: 110m);

        sim.CompletedCrossings.Should().Be(1);
        sim.Breakouts.Should().Be(0);
        sim.Halted.Should().BeFalse();
        sim.TotalRealizedPnl.Should().Be(11m); // sold 1.1 token0 at 110, bought at 100
        sim.Events.Select(e => e.Type).Should().Equal(ChannelEventType.Open, ChannelEventType.CloseAtTop);
    }

    [Fact]
    public void Breakout_down_is_marked_to_market_at_a_loss()
    {
        var sim = ChannelSimulator.Run(Policy(), [100m, 90m], Zeros(2), totalCapital: 110m);

        sim.Breakouts.Should().Be(1);
        sim.CompletedCrossings.Should().Be(0);
        sim.TotalRealizedPnl.Should().Be(-11m); // holds 1.1 token0, now worth 90 each
        sim.Events.Select(e => e.Type).Should().Equal(ChannelEventType.Open, ChannelEventType.BreakoutDown);
    }

    [Fact]
    public void Does_not_reopen_below_the_no_reopen_floor()
    {
        // Opens at 100, breaks at 90, then price 70 is below the floor (80) -> halt, no knife-catching.
        var sim = ChannelSimulator.Run(Policy(floor: 80m), [100m, 90m, 70m], Zeros(3), totalCapital: 110m);

        sim.Halted.Should().BeTrue();
        sim.Events[^1].Type.Should().Be(ChannelEventType.HaltedBelowFloor);
        sim.Events.Count(e => e.Type == ChannelEventType.Open).Should().Be(1); // never reopened at 70
    }

    [Fact]
    public void Halts_after_max_reopens_without_a_full_crossing()
    {
        // Oscillates base->breakout repeatedly above the floor; with max=1 it opens twice then halts.
        var sim = ChannelSimulator.Run(Policy(maxReopens: 1, floor: 50m), [100m, 90m, 100m, 90m, 100m, 90m], Zeros(6), totalCapital: 110m);

        sim.Halted.Should().BeTrue();
        sim.Events[^1].Type.Should().Be(ChannelEventType.HaltedMaxReopens);
        sim.Events.Count(e => e.Type == ChannelEventType.Open).Should().Be(2); // 1 primary + 1 reopen
        sim.Breakouts.Should().Be(2);
    }

    [Fact]
    public void Reports_the_full_series_including_breakouts_not_the_good_run()
    {
        // A good crossing (+11) followed by a breakout (-11): both must appear; total nets to 0.
        var sim = ChannelSimulator.Run(Policy(), [100m, 121m, 100m, 90m], Zeros(4), totalCapital: 110m);

        sim.Events.Select(e => e.Type).Should().Equal(
            ChannelEventType.Open, ChannelEventType.CloseAtTop, ChannelEventType.Open, ChannelEventType.BreakoutDown);
        sim.CompletedCrossings.Should().Be(1);
        sim.Breakouts.Should().Be(1);
        sim.TotalRealizedPnl.Should().Be(0m);
    }

    [Fact]
    public void A_crossing_still_fires_after_losses_decisions_ignore_pnl()
    {
        // Two breakouts (deeply negative running PnL) then a crossing: the crossing still triggers on
        // price >= top, proving open/close decisions are price-driven, not PnL-gated (§6.6).
        var sim = ChannelSimulator.Run(Policy(maxReopens: 5, floor: 50m), [100m, 90m, 100m, 90m, 100m, 121m], Zeros(6), totalCapital: 110m);

        sim.Breakouts.Should().Be(2);
        sim.CompletedCrossings.Should().Be(1);
        sim.Halted.Should().BeFalse();
    }

    [Fact]
    public void Fees_add_to_cycle_pnl()
    {
        var sim = ChannelSimulator.Run(Policy(), [100m, 121m], [0m, 5m], totalCapital: 110m);

        sim.TotalFees.Should().Be(5m);
        sim.TotalRealizedPnl.Should().Be(16m); // 11 principal + 5 fees
    }

    [Fact]
    public void Position_open_at_series_end_is_reported_as_unrealized()
    {
        var sim = ChannelSimulator.Run(Policy(), [100m, 110m], Zeros(2), totalCapital: 110m);

        sim.TotalRealizedPnl.Should().Be(0m);
        sim.UnrealizedPnlAtEnd.Should().BeGreaterThan(0m); // in-the-money mid-channel
        sim.Events.Select(e => e.Type).Should().Equal(ChannelEventType.Open);
    }

    [Fact]
    public void Policy_validates_band_cap_and_floor()
    {
        var badBand = () => new ChannelPolicy(121m, 100m, 1m, 1, 80m);
        var badCap = () => new ChannelPolicy(100m, 121m, 0m, 1, 80m);
        var badFloor = () => new ChannelPolicy(100m, 121m, 1m, 1, 0m);
        badBand.Should().Throw<ArgumentException>();
        badCap.Should().Throw<ArgumentOutOfRangeException>();
        badFloor.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Events_cannot_be_cast_to_a_mutable_collection()
    {
        var sim = ChannelSimulator.Run(Policy(), [100m, 121m], Zeros(2), totalCapital: 110m);

        // The official series must not be adulterable after the fact.
        (sim.Events as ChannelEvent[]).Should().BeNull();
        (sim.Events as List<ChannelEvent>).Should().BeNull();
    }

    [Fact]
    public void Run_validates_series_and_capital()
    {
        var mismatched = () => ChannelSimulator.Run(Policy(), [100m, 121m], Zeros(1), 110m);
        var badCapital = () => ChannelSimulator.Run(Policy(), [100m], Zeros(1), 0m);
        mismatched.Should().Throw<ArgumentException>();
        badCapital.Should().Throw<ArgumentOutOfRangeException>();
    }
}
