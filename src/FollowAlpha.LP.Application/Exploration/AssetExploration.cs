using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Errors;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Domain.Signals;

namespace FollowAlpha.LP.Application.Exploration;

/// <summary>UC-02 `/assets`: the watchlist with each asset's current regime (label + evidence threshold) and RV summary.</summary>
public sealed class ListWatchlistAssets(IExplorationReadStore reads, IPriceStore prices, IClock clock, ExplorationPolicy policy)
{
    public async Task<IReadOnlyList<AssetSummary>> RunAsync(CancellationToken cancellationToken = default)
    {
        var tenant = Tenancy.DefaultTenantId;
        var assets = await reads.GetWatchlistAssetsAsync(tenant, cancellationToken);
        var rows = new List<AssetSummary>(assets.Count);

        foreach (var asset in assets)
        {
            var bars = await prices.GetByAssetAsync(tenant, asset.Id, policy.PriceResolution, cancellationToken);
            var closes = bars.Select(b => b.Close).ToList();
            DateTimeOffset? asOf = bars.Count > 0 ? bars[^1].OpenTimeUtc : null;
            var fresh = asOf is { } t && clock.UtcNow - t <= policy.PriceBarStaleAfter;

            if (fresh && RegimeClassifier.HasEnoughData(closes.Count, policy.Regime))
            {
                var regime = RegimeClassifier.Classify(closes, policy.Regime).Regime;
                rows.Add(new AssetSummary(asset.Id, asset.Symbol, asset.ChainId, ExplorationWire.Regime(regime),
                    ExplorationMetrics.RvSummary(closes, policy), asOf, ExplorationWire.DataStatus.Ok));
            }
            else
            {
                rows.Add(new AssetSummary(asset.Id, asset.Symbol, asset.ChainId, null,
                    new RvSummary(null, null, null), asOf, ExplorationWire.DataStatus.Insufficient));
            }
        }

        return rows;
    }
}

/// <summary>UC-02 `/assets/{id}/chart` (staged): price series + regime timeline + rv-vs-pool-IV. Null = unknown asset (404); 422 if no bars.</summary>
public sealed class GetAssetChart(IExplorationReadStore reads, IPriceStore prices, ISnapshotStore snapshots, IClock clock, ExplorationPolicy policy)
{
    public async Task<AssetChart?> RunAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var tenant = Tenancy.DefaultTenantId;
        if (await reads.GetAssetAsync(tenant, assetId, cancellationToken) is null)
        {
            return null;
        }

        var bars = await prices.GetByAssetAsync(tenant, assetId, policy.PriceResolution, cancellationToken);
        if (bars.Count == 0)
        {
            throw new InsufficientDataException("No price bars have been collected for this asset yet.", ["priceBars"]);
        }

        var closes = bars.Select(b => b.Close).ToList();
        var candles = bars
            .Select(b => new Candle(b.OpenTimeUtc, ExplorationMetrics.Money(b.Open), ExplorationMetrics.Money(b.High),
                ExplorationMetrics.Money(b.Low), ExplorationMetrics.Money(b.Close), ExplorationMetrics.Money(b.Volume)))
            .ToList();

        var timeline = new List<RegimePoint>();
        for (var end = policy.Regime.MinBars; end <= closes.Count; end++)
        {
            var regime = RegimeClassifier.Classify(closes.GetRange(0, end), policy.Regime).Regime;
            timeline.Add(new RegimePoint(bars[end - 1].OpenTimeUtc, ExplorationWire.Regime(regime)));
        }

        var rvVsPoolIv = new RvVsPoolIv(
            ExplorationMetrics.RvSummary(closes, policy),
            await AveragePoolIvAsync(tenant, assetId, cancellationToken),
            ExplorationWire.IvBasisPoolTvlTotal,
            bars[^1].OpenTimeUtc,
            "Descriptive: realized vol vs the IV the asset's pools pay (basis pool_tvl_total). No cheap/expensive verdict in 3.2.");

        return new AssetChart(candles, timeline, rvVsPoolIv);
    }

    // Mean pool IV across the asset's pools that have a fresh snapshot with positive TVL; null when none qualify.
    private async Task<decimal?> AveragePoolIvAsync(string tenant, string assetId, CancellationToken cancellationToken)
    {
        var pools = await reads.GetPoolsForAssetAsync(tenant, assetId, cancellationToken);
        var ivs = new List<decimal>();
        foreach (var pool in pools)
        {
            var snap = await snapshots.GetLatestPoolSnapshotAsync(tenant, pool.Id, cancellationToken);
            if (snap is null || clock.UtcNow - snap.AsOfUtc > policy.SnapshotStaleAfter)
            {
                continue;
            }

            if (ExplorationMetrics.Iv(pool, snap).Annualized is { } annualized)
            {
                ivs.Add(annualized);
            }
        }

        return ivs.Count > 0 ? ivs.Average() : null;
    }
}

/// <summary>UC-02 `/assets/{id}/regime`: descriptive regime + full evidence (RN-07, never direction). Null = unknown asset (404); 422 if thin/stale.</summary>
public sealed class ClassifyAssetRegime(IExplorationReadStore reads, IPriceStore prices, IClock clock, ExplorationPolicy policy)
{
    public async Task<RegimeReport?> RunAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var tenant = Tenancy.DefaultTenantId;
        if (await reads.GetAssetAsync(tenant, assetId, cancellationToken) is null)
        {
            return null;
        }

        var bars = await prices.GetByAssetAsync(tenant, assetId, policy.PriceResolution, cancellationToken);
        var closes = bars.Select(b => b.Close).ToList();

        if (!RegimeClassifier.HasEnoughData(closes.Count, policy.Regime))
        {
            throw new InsufficientDataException(
                $"At least {policy.Regime.MinBars} daily bars are required to classify a regime; have {closes.Count}.", ["priceBars"]);
        }

        var asOf = bars[^1].OpenTimeUtc;
        if (clock.UtcNow - asOf > policy.PriceBarStaleAfter)
        {
            throw new InsufficientDataException(
                $"Latest price bar ({asOf:O}) is stale beyond {policy.PriceBarStaleAfter}; not a current signal.", ["freshPriceBar"]);
        }

        var result = RegimeClassifier.Classify(closes, policy.Regime);
        var e = result.Evidence;
        return new RegimeReport(
            ExplorationWire.Regime(result.Regime),
            new RegimeEvidenceDto(e.RvPercentile, e.Trendiness, e.RvWindow, e.PercentileLookback, e.TrendinessWindow, e.MinBars, e.SampleCount, asOf, e.ClassificationReason));
    }
}
