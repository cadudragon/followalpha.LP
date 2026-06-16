using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FollowAlpha.LP.Application.Pools;
using FollowAlpha.LP.Application.Protocols;

namespace FollowAlpha.LP.Infrastructure.TheGraph;

/// <summary>
/// <see cref="IPoolDataSource"/> over the The Graph decentralized gateway (ARCHITECTURE.md §6;
/// TECH-STACK.md §2). Plain <see cref="HttpClient"/> + static GraphQL query strings with variables (no
/// GraphQL client library); only the fields used are requested; the tick distribution is paginated by a
/// progressive <c>tickIdx</c> cursor (no high <c>skip</c>). HTTP resilience (retry/backoff) is attached
/// to the injected <see cref="HttpClient"/> at the composition root (Phase 2.4), not here. The API key
/// is read from options and never logged.
/// </summary>
public sealed class TheGraphPoolDataSource(
    HttpClient httpClient,
    IDexProtocolRegistry registry,
    TheGraphGatewayOptions options) : IPoolDataSource
{
    private const string PoolStateQuery =
        "query($id: ID!) { pool(id: $id) { tick sqrtPrice liquidity feeTier totalValueLockedUSD } }";

    private const string DayVolumeQuery =
        "query($id: ID!, $days: Int!) { pool(id: $id) { poolDayData(first: $days, orderBy: date, orderDirection: desc) { date volumeUSD } } }";

    private const string TicksQuery =
        "query($pool: String!, $lastTick: BigInt!, $pageSize: Int!) { ticks(first: $pageSize, where: { pool: $pool, tickIdx_gt: $lastTick }, orderBy: tickIdx, orderDirection: asc) { tickIdx liquidityNet liquidityGross } }";

    // One below the Uniswap v3 minimum tick, so the first page starts at the bottom of the distribution.
    private const string TickCursorStart = "-887273";

    public async Task<PoolState> GetPoolStateAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default)
    {
        var id = NormalizeAddress(poolAddress);
        var data = await ExecuteAsync<PoolStateResponse>(chainId, PoolStateQuery, new { id }, cancellationToken);
        var pool = data.Pool ?? throw new PoolNotFoundException(chainId, poolAddress);

        return new PoolState(
            poolAddress,
            ParseInt(pool.Tick),
            pool.SqrtPrice,
            pool.Liquidity,
            ParseInt(pool.FeeTier),
            ParseDecimal(pool.TotalValueLockedUsd));
    }

    public async Task<IReadOnlyList<PoolDayVolume>> GetDayVolumesAsync(string chainId, string poolAddress, int days, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be strictly positive.");
        }

        var id = NormalizeAddress(poolAddress);
        var data = await ExecuteAsync<DayVolumeResponse>(chainId, DayVolumeQuery, new { id, days }, cancellationToken);
        var pool = data.Pool ?? throw new PoolNotFoundException(chainId, poolAddress);

        return [.. pool.PoolDayData.Select(d =>
            new PoolDayVolume(DateTimeOffset.FromUnixTimeSeconds(d.Date), ParseDecimal(d.VolumeUsd)))];
    }

    public async Task<IReadOnlyList<TickLiquidity>> GetTickLiquidityAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default)
    {
        var pool = NormalizeAddress(poolAddress);
        var pageSize = options.TickPageSize;
        var cursor = TickCursorStart;
        var all = new List<TickLiquidity>();

        while (true)
        {
            var data = await ExecuteAsync<TicksResponse>(
                chainId, TicksQuery, new { pool, lastTick = cursor, pageSize }, cancellationToken);

            foreach (var tick in data.Ticks)
            {
                all.Add(new TickLiquidity(ParseInt(tick.TickIdx), tick.LiquidityNet, tick.LiquidityGross));
            }

            if (data.Ticks.Count < pageSize)
            {
                break;
            }

            cursor = data.Ticks[^1].TickIdx;
        }

        return all;
    }

    private async Task<TData> ExecuteAsync<TData>(string chainId, string query, object variables, CancellationToken cancellationToken)
    {
        var uri = BuildUri(chainId);
        var payload = JsonSerializer.Serialize(new { query, variables });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(uri, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GraphResponse<TData>>(body);

        if (result?.Errors is { Count: > 0 } errors)
        {
            throw new TheGraphQueryException(string.Join("; ", errors.Select(e => e.Message)));
        }

        return result is { Data: not null }
            ? result.Data
            : throw new TheGraphQueryException("The Graph returned no data.");
    }

    private Uri BuildUri(string chainId)
    {
        var descriptor = registry.GetByChain(chainId);
        var idSegment = !string.IsNullOrEmpty(descriptor.SubgraphDeploymentId)
            ? $"deployments/id/{descriptor.SubgraphDeploymentId}"
            : $"subgraphs/id/{descriptor.SubgraphId}";
        return new Uri($"{options.BaseUrl.TrimEnd('/')}/{options.ApiKey}/{idSegment}");
    }

    // The Graph keys pools by their lowercase hex address.
#pragma warning disable CA1308 // subgraph entity ids are lowercase by definition
    private static string NormalizeAddress(string address) => address.ToLowerInvariant();
#pragma warning restore CA1308

    private static int ParseInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private sealed record GraphResponse<TData>(
        [property: JsonPropertyName("data")] TData? Data,
        [property: JsonPropertyName("errors")] IReadOnlyList<GraphQlError>? Errors);

    private sealed record GraphQlError([property: JsonPropertyName("message")] string Message);

    private sealed record PoolStateResponse([property: JsonPropertyName("pool")] PoolStateNode? Pool);

    private sealed record PoolStateNode(
        [property: JsonPropertyName("tick")] string Tick,
        [property: JsonPropertyName("sqrtPrice")] string SqrtPrice,
        [property: JsonPropertyName("liquidity")] string Liquidity,
        [property: JsonPropertyName("feeTier")] string FeeTier,
        [property: JsonPropertyName("totalValueLockedUSD")] string TotalValueLockedUsd);

    private sealed record DayVolumeResponse([property: JsonPropertyName("pool")] DayVolumePoolNode? Pool);

    private sealed record DayVolumePoolNode(
        [property: JsonPropertyName("poolDayData")] IReadOnlyList<DayVolumeNode> PoolDayData);

    private sealed record DayVolumeNode(
        [property: JsonPropertyName("date")] long Date,
        [property: JsonPropertyName("volumeUSD")] string VolumeUsd);

    private sealed record TicksResponse([property: JsonPropertyName("ticks")] IReadOnlyList<TickNode> Ticks);

    private sealed record TickNode(
        [property: JsonPropertyName("tickIdx")] string TickIdx,
        [property: JsonPropertyName("liquidityNet")] string LiquidityNet,
        [property: JsonPropertyName("liquidityGross")] string LiquidityGross);
}
