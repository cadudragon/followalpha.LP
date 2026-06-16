namespace FollowAlpha.LP.Infrastructure.TheGraph;

/// <summary>
/// Configuration for the The Graph decentralized gateway. <see cref="ApiKey"/> comes from
/// env/user-secrets (<c>GRAPH_API_KEY</c>), never the repo. The gateway URL is built per request from
/// the descriptor (subgraph id or deployment id); the key is placed in the URL path per the gateway's
/// scheme and is never logged.
/// </summary>
public sealed class TheGraphGatewayOptions
{
    /// <summary>Gateway base URL (the decentralized gateway).</summary>
    public string BaseUrl { get; set; } = "https://gateway.thegraph.com/api";

    /// <summary>The gateway API key (from <c>GRAPH_API_KEY</c>).</summary>
    public required string ApiKey { get; set; }

    /// <summary>Page size for tick-liquidity cursor pagination (avoids high <c>skip</c>).</summary>
    public int TickPageSize { get; set; } = 1000;
}
