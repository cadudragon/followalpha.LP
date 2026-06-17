namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// RPC endpoints per chain for the event reader. Bound at the composition root from env/user-secrets
/// (<c>RPC_URL_ARBITRUM</c>, <c>RPC_URL_BASE</c>, or an Alchemy URL with <c>ALCHEMY_API_KEY</c>) — never
/// the repo. Tests do not use this (they inject a fake <see cref="IEvmRpc"/>).
/// </summary>
public sealed class EvmRpcOptions
{
    /// <summary>chainId → RPC URL.</summary>
    public IDictionary<string, string> RpcUrls { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Max block span per <c>eth_getLogs</c> request. Wide ranges (e.g. <c>fromBlock=0</c>) are split into
    /// chunks of at most this size and the results concatenated, so a full-history scan does not exceed
    /// provider range/response limits (Alchemy etc.). Default 50k.
    /// </summary>
    public long MaxBlockSpan { get; init; } = 50_000;
}
