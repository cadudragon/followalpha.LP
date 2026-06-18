using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Prices;

namespace FollowAlpha.LP.Application.Collection;

/// <summary>A watchlist asset to refresh price for (the DataSync worker supplies these from the seeded assets).</summary>
public sealed record AssetToPrice(string AssetId, string ChainId, string TokenAddress);

/// <summary>Per-asset outcome of a price refresh (for the DataSync worker's structured per-job log, NFR O2).</summary>
public sealed record PriceIngestionOutcome(string AssetId, int BarsRead, int BarsInserted, string? Error);

/// <summary>
/// Ingestion use case (ARCHITECTURE.md §5): for each watchlist asset, fetch the recent daily USD OHLCV
/// from <see cref="IPriceSeriesSource"/> and persist it as append-only <see cref="PriceBar"/> facts.
/// Idempotent — bars are insert-if-absent on (AssetId, Resolution, OpenTimeUtc), so re-running a window
/// re-inserts nothing. Daily bars are recoverable (unlike the tick distribution), so a missed run is not
/// fatal. The use case never throws for one bad asset: it records the error and continues.
/// </summary>
public sealed class IngestPriceSeries(IPriceSeriesSource source, IPriceStore store)
{
    /// <summary>The bar resolution this job ingests (daily). The natural key includes it, so other resolutions coexist.</summary>
    public const string Resolution = "1d";
    private const string Source = "thegraph";

    public async Task<IReadOnlyList<PriceIngestionOutcome>> RunAsync(
        IReadOnlyCollection<AssetToPrice> assets, int days, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);

        var outcomes = new List<PriceIngestionOutcome>(assets.Count);

        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                outcomes.Add(await IngestAssetAsync(asset, days, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                outcomes.Add(new PriceIngestionOutcome(asset.AssetId, BarsRead: 0, BarsInserted: 0, Error: ex.Message));
            }
        }

        return outcomes;
    }

    private async Task<PriceIngestionOutcome> IngestAssetAsync(AssetToPrice asset, int days, CancellationToken cancellationToken)
    {
        var bars = await source.GetDailyUsdBarsAsync(asset.ChainId, asset.TokenAddress, days, cancellationToken);

        var inserted = 0;
        foreach (var bar in bars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await store.InsertIfAbsentAsync(
                    new PriceBar
                    {
                        AssetId = asset.AssetId,
                        Resolution = Resolution,
                        OpenTimeUtc = bar.DayStartUtc,
                        Open = bar.Open,
                        High = bar.High,
                        Low = bar.Low,
                        Close = bar.Close,
                        Volume = bar.VolumeUsd,
                        Source = Source,
                    },
                    cancellationToken))
            {
                inserted++;
            }
        }

        return new PriceIngestionOutcome(asset.AssetId, bars.Count, inserted, Error: null);
    }
}
