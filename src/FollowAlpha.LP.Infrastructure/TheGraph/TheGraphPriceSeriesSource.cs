using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FollowAlpha.LP.Application.Prices;
using FollowAlpha.LP.Application.Protocols;

namespace FollowAlpha.LP.Infrastructure.TheGraph;

/// <summary>
/// <see cref="IPriceSeriesSource"/> over the The Graph decentralized gateway: the Uniswap v3 native
/// <c>tokenDayData</c> entity, whose <c>open/high/low/close</c> are the token's USD price and
/// <c>volumeUSD</c> its daily USD volume. Same transport shape as <see cref="TheGraphPoolDataSource"/>
/// (plain HttpClient + static query, key from options, never logged); kept as a separate typed client so
/// each adapter gets its own resilience pipeline at the composition root.
/// </summary>
public sealed class TheGraphPriceSeriesSource(
    HttpClient httpClient,
    IDexProtocolRegistry registry,
    TheGraphGatewayOptions options) : IPriceSeriesSource
{
    private const string TokenDayDataQuery =
        "query($token: String!, $days: Int!) { tokenDayDatas(first: $days, orderBy: date, orderDirection: desc, where: { token: $token }) { date open high low close volumeUSD } }";

    public async Task<IReadOnlyList<AssetUsdBar>> GetDailyUsdBarsAsync(
        string chainId, string tokenAddress, int days, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be strictly positive.");
        }

        var token = tokenAddress.ToLowerInvariant();
        var data = await ExecuteAsync<TokenDayDataResponse>(chainId, TokenDayDataQuery, new { token, days }, cancellationToken);

        // The query is newest-first; the port contract is ascending by day.
        return [.. data.TokenDayDatas
            .Select(d => new AssetUsdBar(
                DateTimeOffset.FromUnixTimeSeconds(d.Date),
                ParseDecimal(d.Open),
                ParseDecimal(d.High),
                ParseDecimal(d.Low),
                ParseDecimal(d.Close),
                ParseDecimal(d.VolumeUsd)))
            .OrderBy(b => b.DayStartUtc)];
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

    private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private sealed record GraphResponse<TData>(
        [property: JsonPropertyName("data")] TData? Data,
        [property: JsonPropertyName("errors")] IReadOnlyList<GraphQlError>? Errors);

    private sealed record GraphQlError([property: JsonPropertyName("message")] string Message);

    private sealed record TokenDayDataResponse(
        [property: JsonPropertyName("tokenDayDatas")] IReadOnlyList<TokenDayDataNode> TokenDayDatas);

    private sealed record TokenDayDataNode(
        [property: JsonPropertyName("date")] long Date,
        [property: JsonPropertyName("open")] string Open,
        [property: JsonPropertyName("high")] string High,
        [property: JsonPropertyName("low")] string Low,
        [property: JsonPropertyName("close")] string Close,
        [property: JsonPropertyName("volumeUSD")] string VolumeUsd);
}
