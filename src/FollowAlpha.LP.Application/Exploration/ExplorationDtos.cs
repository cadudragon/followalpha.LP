namespace FollowAlpha.LP.Application.Exploration;

// Wire-facing results for UC-02 exploration. Money/raw on-chain integers are strings (API-CONTRACT §1);
// vol fractions, ratios, percentiles and ticks are numbers; regime/dataStatus are the contract's string
// constants. Timestamps are DateTimeOffset (serialized ISO-8601 UTC). Frozen in API-CONTRACT.md (3.2).

/// <summary>Annualized realized vol over the short/medium/long windows; a window is null when history is too thin.</summary>
public sealed record RvSummary(decimal? D7, decimal? D30, decimal? D90);

/// <summary>One `/assets` watchlist row. `Regime` is null and metrics absent when `DataStatus = "INSUFFICIENT"`.</summary>
public sealed record AssetSummary(
    string Id, string Symbol, string Chain, string? Regime, RvSummary RvSummary, DateTimeOffset? AsOfUtc, string DataStatus);

public sealed record Candle(DateTimeOffset OpenTimeUtc, string Open, string High, string Low, string Close, string VolumeUsd);

public sealed record RegimePoint(DateTimeOffset AsOfUtc, string Regime);

public sealed record RvVsPoolIv(RvSummary RealizedVol, decimal? PoolIvAverage, string IvBasis, DateTimeOffset? AsOfUtc, string Note);

public sealed record AssetChart(IReadOnlyList<Candle> Candles, IReadOnlyList<RegimePoint> RegimeTimeline, RvVsPoolIv RvVsPoolIv);

public sealed record RegimeEvidenceDto(
    decimal RvPercentile, decimal Trendiness,
    int RvWindow, int PercentileLookback, int TrendinessWindow, int MinBars, int SampleCount,
    DateTimeOffset AsOfUtc, string ClassificationReason);

public sealed record RegimeReport(string Regime, RegimeEvidenceDto Evidence);

/// <summary>Pool IV as a declared object, never a bare number: the value plus its basis and reproducible inputs.</summary>
public sealed record PoolIvDto(decimal? Annualized, string Basis, string? VolumeUsd, string? TvlUsd, DateTimeOffset? AsOfUtc);

/// <summary>Competing liquidity as a declared object: raw L figures (not USD), the band, and its tick bounds.</summary>
public sealed record CompetingLiquidityDto(
    string? ActiveLiquidityAtCurrentTickRaw, string? LiquidityDensityAroundPriceRaw,
    decimal BandPct, int? TickLower, int? TickUpper, DateTimeOffset? AsOfUtc);

public sealed record PoolComparisonRow(
    string PoolId, string Pair, string Chain, int FeeTier, DateTimeOffset? AsOfUtc, string DataStatus,
    string? VolumeUsd, string? TvlUsd, decimal? VolTvlRatio, PoolIvDto PoolIv, CompetingLiquidityDto CompetingLiquidity);

public sealed record PoolSnapshotDto(
    DateTimeOffset AsOfUtc, int CurrentTick, string SqrtPriceX96, string Liquidity, string TvlUsd, string DayVolumeUsd, string Source);

public sealed record TickLiquidityDto(int Tick, string LiquidityNet, string LiquidityGross);

public sealed record PoolDetail(
    string PoolId, string Pair, string Chain, int FeeTier, int TickSpacing,
    PoolSnapshotDto LatestSnapshot, decimal? VolTvlRatio, PoolIvDto PoolIv,
    CompetingLiquidityDto CompetingLiquidity, IReadOnlyList<TickLiquidityDto> TickLiquidity);

/// <summary>The contract's string constants for regime + pool/list data status (kept out of enum serialization).</summary>
public static class ExplorationWire
{
    public const string IvBasisPoolTvlTotal = "pool_tvl_total";

    public static string Regime(Domain.Signals.Regime regime) => regime switch
    {
        Domain.Signals.Regime.Range => "RANGE",
        Domain.Signals.Regime.Trending => "TRENDING",
        Domain.Signals.Regime.Transition => "TRANSITION",
        _ => throw new ArgumentOutOfRangeException(nameof(regime), regime, "Unknown regime."),
    };

    public static class DataStatus
    {
        public const string Ok = "OK";
        public const string Insufficient = "INSUFFICIENT";
        public const string Stale = "STALE";
        public const string NoSnapshot = "NO_SNAPSHOT";
    }
}
