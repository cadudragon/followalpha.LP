using System.Runtime.CompilerServices;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Protocols;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using FollowAlpha.LP.Infrastructure.Protocols;
using FluentAssertions;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents.Recorded;

/// <summary>
/// Proves the production <see cref="NethereumEvmRpc"/> path (eth_getLogs, ERC-721 Transfer discovery,
/// <c>positions</c>, factory <c>getPool</c>, ERC-20 <c>decimals</c>, receipt, block) against <b>real</b>
/// captured public JSON-RPC data, replayed at the Nethereum transport level (no network, no key). The
/// capture is a real Uniswap v3 position of the audit wallet (config/wallets.json) on Arbitrum.
/// A separate, env-gated smoke test re-records the fixture and asserts against the live public RPC.
/// </summary>
public class RecordedRpcReplayTests
{
    private const string ChainId = "arbitrum";
    private const string Wallet = "0xa3660aBb49644876714611122b1618faA07e0281";
    private const string TokenId = "5531934";
    private const string Weth = "0x82af49447d8a07e3bd95bd0d56f35241523fbab1";
    private const string Usdc = "0xaf88d065e77c8cc2239327c5edb3a432268e5831";
    private const string Pool = "0xc6962004f452be9203591991d15f6b388e09e8d0";
    private const string PublicArbitrumRpc = "https://arb1.arbitrum.io/rpc";
    private const string FixtureName = "arbitrum_position_5531934.json";

    private static IDexProtocolRegistry Registry => new ConfiguredDexProtocolRegistry(DefaultDexProtocols.UniswapV3);

    [Fact]
    public async Task Replays_real_public_capture_through_the_real_nethereum_path_offline()
    {
        var fixture = JObject.Parse(File.ReadAllText(FixturePath()));
        // Unbounded span: the fixture records one eth_getLogs per topic set (keyed by topics, not range),
        // so the replay must issue a single call per topic — not the production 50k-block chunking.
        var rpc = new NethereumEvmRpc(_ => new Web3(new RecordedRpcClient(fixture)), maxBlockSpan: long.MaxValue);

        await AssertReadsRealPositionAsync(rpc);
    }

    [Fact]
    public async Task Live_public_rpc_smoke_and_capture()
    {
        // Opt-in only (no network / no key on the default path). Set FOLLOWALPHA_RPC_SMOKE=1 to run; this
        // hits the public RPC, asserts the live values, and rewrites the committed offline fixture.
        if (Environment.GetEnvironmentVariable("FOLLOWALPHA_RPC_SMOKE") != "1")
        {
            return;
        }

        using var recorder = new RecordingRpcClient(PublicArbitrumRpc);
        var rpc = new NethereumEvmRpc(_ => new Web3(recorder), maxBlockSpan: long.MaxValue);

        await AssertReadsRealPositionAsync(rpc);

        await File.WriteAllTextAsync(FixturePath(), recorder.Recorded.ToString());
    }

    private static async Task AssertReadsRealPositionAsync(IEvmRpc rpc)
    {
        var reader = new EvmRpcEventReader(Registry, rpc);
        var stateReader = new EvmRpcPositionStateReader(Registry, rpc);

        var head = await reader.GetChainHeadBlockAsync(ChainId);
        head.Should().BeGreaterThan(0);

        var tokenIds = await reader.DiscoverWalletTokenIdsAsync(ChainId, Wallet, 0, head);
        tokenIds.Should().Contain(TokenId);

        var events = await reader.ReadPositionEventsAsync(ChainId, [TokenId], 0, head);
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.TokenId == TokenId);
        events.Should().Contain(e => e.EventType == PositionEventTypes.Mint);

        var state = await stateReader.GetPositionStateAsync(ChainId, TokenId);
        state.Token0.Should().Be(Weth);
        state.Token1.Should().Be(Usdc);
        state.FeeTier.Should().Be(500);
        state.TickLower.Should().BeLessThan(state.TickUpper);

        var pool = await stateReader.GetPoolAddressAsync(ChainId, state.Token0, state.Token1, state.FeeTier);
        pool.Should().Be(Pool);

        (await stateReader.GetTokenDecimalsAsync(ChainId, state.Token0)).Should().Be(18);
        (await stateReader.GetTokenDecimalsAsync(ChainId, state.Token1)).Should().Be(6);
    }

    private static string FixturePath([CallerFilePath] string callerPath = "") =>
        Path.Combine(Path.GetDirectoryName(callerPath)!, "Fixtures", FixtureName);
}
