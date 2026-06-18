using System.Numerics;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Pools;
using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Application.Collection;

/// <summary>A wallet to sync on one chain. <paramref name="FromBlock"/> is the configured floor; the cursor
/// resumes from there. <paramref name="ToBlock"/> is the chain head; <paramref name="ReorgBuffer"/> rewinds
/// the resume point to absorb shallow reorgs (L2 finalizes fast).</summary>
public sealed record WalletSyncRequest(
    string WalletId, string WalletAddress, string ChainId, long FromBlock, long ToBlock, int ReorgBuffer = 0);

/// <summary>Outcome of a wallet sync (for the DataSync worker's structured per-job log, NFR O2).</summary>
public sealed record WalletSyncOutcome(
    string WalletId,
    long FromBlock,
    long ToBlock,
    int TransfersSeen,
    int TokenIdsOwned,
    int EventsRead,
    int EventsInserted,
    int EventsSkippedNotOwned);

/// <summary>
/// Ingestion use case (ARCHITECTURE.md §5): incrementally sync a wallet's NPM position events into
/// append-only <see cref="PositionEvent"/> facts, attributed by <b>owner-at-time</b>.
/// <list type="number">
/// <item>Resume from the persisted <see cref="WalletSyncCursor"/> (minus a reorg buffer), so the job does
/// not rescan from genesis every run.</item>
/// <item>Discover the wallet's ERC-721 <c>Transfer</c>s (in and out) over the window and fold them into
/// <see cref="WalletPositionOwnership"/> intervals — idempotent on re-scan via the transfer's
/// <c>(block, logIndex)</c>.</item>
/// <item>Read the raw mint/burn/collect events for the owned tokenIds, and persist only those whose
/// <c>(block, logIndex)</c> falls <b>inside</b> an ownership interval. Events outside any interval (the NFT
/// was transferred out, or pre-acquisition) are skipped and counted — never written with a wrong wallet,
/// which would poison the append-only audit truth (decided 2026-06-17).</item>
/// <item>Advance the cursor to the window's end — only after the window completes.</item>
/// </list>
/// Enrichment (range, pool, decimals) resolves via <see cref="IPositionStateReader"/>; native gas is raw,
/// USD deferred. Insert-if-absent on (chain, tx, logIndex) keeps re-syncs free of duplicates.
/// </summary>
public sealed class SyncWalletPositionEvents(
    IChainEventReader eventReader,
    IPositionStateReader stateReader,
    IPositionEventStore store,
    IWalletOwnershipStore ownership,
    IWalletSyncCursorStore cursors)
{
    public async Task<WalletSyncOutcome> RunAsync(WalletSyncRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = Tenancy.DefaultTenantId;
        var fromBlock = await ResolveResumeBlockAsync(tenantId, request, cancellationToken);
        if (fromBlock > request.ToBlock)
        {
            return Empty(request, fromBlock);
        }

        var transfers = await eventReader.DiscoverWalletTransfersAsync(
            request.ChainId, request.WalletAddress, fromBlock, request.ToBlock, cancellationToken);

        var intervalsByToken = await UpdateOwnershipAsync(tenantId, request, transfers, cancellationToken);
        if (intervalsByToken.Count == 0)
        {
            await cursors.SetAsync(Cursor(tenantId, request), cancellationToken);
            return new WalletSyncOutcome(request.WalletId, fromBlock, request.ToBlock, transfers.Count, 0, 0, 0, 0);
        }

        var events = await eventReader.ReadPositionEventsAsync(
            request.ChainId, [.. intervalsByToken.Keys], fromBlock, request.ToBlock, cancellationToken);

        var (inserted, skipped) = await PersistOwnedEventsAsync(request, events, intervalsByToken, cancellationToken);

        await cursors.SetAsync(Cursor(tenantId, request), cancellationToken);
        return new WalletSyncOutcome(
            request.WalletId, fromBlock, request.ToBlock, transfers.Count, intervalsByToken.Count, events.Count, inserted, skipped);
    }

    /// <summary>The block to resume from: the cursor's high-water mark minus the reorg buffer, floored at the configured start.</summary>
    private async Task<long> ResolveResumeBlockAsync(string tenantId, WalletSyncRequest request, CancellationToken cancellationToken)
    {
        var cursor = await cursors.GetAsync(tenantId, request.ChainId, request.WalletId, cancellationToken);
        if (cursor is null)
        {
            return request.FromBlock;
        }

        var resume = cursor.LastScannedBlock + 1 - request.ReorgBuffer;
        return Math.Max(request.FromBlock, resume);
    }

    /// <summary>
    /// Folds the window's transfers into the wallet's ownership intervals (loaded from the store) and
    /// upserts the ones that changed. Idempotent: a transfer already reflected as an interval boundary
    /// (same block+logIndex) is skipped, so an overlapping re-scan does not create phantom intervals.
    /// </summary>
    private async Task<Dictionary<string, List<WalletPositionOwnership>>> UpdateOwnershipAsync(
        string tenantId, WalletSyncRequest request, IReadOnlyList<WalletNftTransfer> transfers, CancellationToken cancellationToken)
    {
        var existing = await ownership.GetByWalletAsync(tenantId, request.ChainId, request.WalletId, cancellationToken);
        var byToken = existing
            .GroupBy(i => i.TokenId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Seq).ToList());

        var touched = new List<WalletPositionOwnership>();

        foreach (var t in transfers.OrderBy(t => t.BlockNumber).ThenBy(t => t.LogIndex))
        {
            if (!byToken.TryGetValue(t.TokenId, out var list))
            {
                list = [];
                byToken[t.TokenId] = list;
            }

            if (t.Direction == TransferDirection.In)
            {
                if (list.Any(i => i.AcquiredBlock == t.BlockNumber && i.AcquiredLogIndex == t.LogIndex))
                {
                    continue; // already recorded (re-scan)
                }

                if (list.Any(i => i.ReleasedBlock is null))
                {
                    continue; // anomalous double-in without an intervening out — keep the earlier interval, don't guess
                }

                var interval = new WalletPositionOwnership
                {
                    TenantId = tenantId,
                    ChainId = request.ChainId,
                    WalletId = request.WalletId,
                    TokenId = t.TokenId,
                    Seq = list.Count == 0 ? 0 : list.Max(i => i.Seq) + 1,
                    AcquiredBlock = t.BlockNumber,
                    AcquiredLogIndex = t.LogIndex,
                };
                list.Add(interval);
                touched.Add(interval);
            }
            else
            {
                if (list.Any(i => i.ReleasedBlock == t.BlockNumber && i.ReleasedLogIndex == t.LogIndex))
                {
                    continue; // already recorded (re-scan)
                }

                var open = list.FirstOrDefault(i => i.ReleasedBlock is null);
                if (open is null)
                {
                    continue; // out without a known in (first sync started after acquisition) — ignore, do not guess an interval
                }

                open.ReleasedBlock = t.BlockNumber;
                open.ReleasedLogIndex = t.LogIndex;
                touched.Add(open);
            }
        }

        foreach (var interval in touched)
        {
            await ownership.UpsertAsync(interval, cancellationToken);
        }

        // Only tokenIds the wallet has ever owned are candidates for event attribution.
        return byToken.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private async Task<(int Inserted, int Skipped)> PersistOwnedEventsAsync(
        WalletSyncRequest request,
        IReadOnlyList<ChainPositionEvent> events,
        Dictionary<string, List<WalletPositionOwnership>> intervalsByToken,
        CancellationToken cancellationToken)
    {
        var enrichmentByToken = new Dictionary<string, PositionEnrichment>();
        var inserted = 0;
        var skipped = 0;

        foreach (var ev in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!intervalsByToken.TryGetValue(ev.TokenId, out var intervals) || !OwnedAt(intervals, ev.BlockNumber, ev.LogIndex))
            {
                skipped++; // not owned by this wallet at this (block, logIndex) — never attribute it
                continue;
            }

            var enrichment = await ResolveEnrichmentAsync(request.ChainId, ev.TokenId, enrichmentByToken, cancellationToken);
            if (await store.InsertIfAbsentAsync(ToPositionEvent(request, ev, enrichment), cancellationToken))
            {
                inserted++;
            }
        }

        return (inserted, skipped);
    }

    /// <summary>True iff <c>(block, logIndex)</c> falls inside an open interval <c>[acquired, released)</c>.</summary>
    private static bool OwnedAt(IEnumerable<WalletPositionOwnership> intervals, long block, int logIndex)
    {
        foreach (var i in intervals)
        {
            var atOrAfterAcquired = block > i.AcquiredBlock || (block == i.AcquiredBlock && logIndex >= i.AcquiredLogIndex);
            if (!atOrAfterAcquired)
            {
                continue;
            }

            if (i.ReleasedBlock is null)
            {
                return true; // still owned
            }

            var beforeReleased = block < i.ReleasedBlock || (block == i.ReleasedBlock && logIndex < i.ReleasedLogIndex);
            if (beforeReleased)
            {
                return true;
            }
        }

        return false;
    }

    private static WalletSyncCursor Cursor(string tenantId, WalletSyncRequest request) => new()
    {
        TenantId = tenantId,
        ChainId = request.ChainId,
        WalletId = request.WalletId,
        LastScannedBlock = request.ToBlock,
    };

    private static WalletSyncOutcome Empty(WalletSyncRequest request, long fromBlock) =>
        new(request.WalletId, fromBlock, request.ToBlock, 0, 0, 0, 0, 0);

    private async Task<PositionEnrichment> ResolveEnrichmentAsync(
        string chainId, string tokenId, Dictionary<string, PositionEnrichment> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(tokenId, out var cached))
        {
            return cached;
        }

        var state = await stateReader.GetPositionStateAsync(chainId, tokenId, cancellationToken);
        var poolAddress = await stateReader.GetPoolAddressAsync(chainId, state.Token0, state.Token1, state.FeeTier, cancellationToken);
        var decimals0 = await stateReader.GetTokenDecimalsAsync(chainId, state.Token0, cancellationToken);
        var decimals1 = await stateReader.GetTokenDecimalsAsync(chainId, state.Token1, cancellationToken);

        var enrichment = new PositionEnrichment(state, PoolIdentity.For(chainId, poolAddress), decimals0, decimals1);
        cache[tokenId] = enrichment;
        return enrichment;
    }

    private static PositionEvent ToPositionEvent(WalletSyncRequest request, ChainPositionEvent ev, PositionEnrichment e)
    {
        // MINT/BURN carry deposited/withdrawn principal in Amount0/1; COLLECT carries collected fees.
        var isCollect = ev.EventType == PositionEventTypes.Collect;
        var amount0 = Human(ev.Amount0Raw, e.Decimals0);
        var amount1 = Human(ev.Amount1Raw, e.Decimals1);

        return new PositionEvent
        {
            ChainId = ev.ChainId,
            TxHash = ev.TxHash,
            LogIndex = ev.LogIndex,
            WalletId = request.WalletId,
            PoolId = e.PoolId,
            EventType = ev.EventType,
            TickLower = e.State.TickLower,
            TickUpper = e.State.TickUpper,
            LiquidityDelta = ev.LiquidityDeltaRaw,
            Amount0 = isCollect ? 0m : amount0,
            Amount1 = isCollect ? 0m : amount1,
            FeesCollected0 = isCollect ? amount0 : 0m,
            FeesCollected1 = isCollect ? amount1 : 0m,
            GasUsed = ev.GasUsed,
            EffectiveGasPriceWei = ev.EffectiveGasPriceWei,
            NativeGasCostWei = ev.NativeGasCostWei,
            GasCostUsd = null, // deferred until a reliable historical price source exists (2026-06-17)
            BlockTimeUtc = ev.BlockTimeUtc,
        };
    }

    // Raw base units → human decimal via the Domain's single conversion point (ARCHITECTURE.md §4.1).
    private static decimal Human(string raw, int decimals) =>
        new TokenAmount(BigInteger.Parse(raw, System.Globalization.CultureInfo.InvariantCulture), decimals).ToDecimal();

    private sealed record PositionEnrichment(ChainPositionState State, string PoolId, int Decimals0, int Decimals1);
}
