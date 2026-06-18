namespace FollowAlpha.LP.Application.ChainEvents;

/// <summary>The three position-event kinds (DATA-MODEL.md), mapped from NonfungiblePositionManager events.</summary>
public static class PositionEventTypes
{
    public const string Mint = "MINT";       // IncreaseLiquidity
    public const string Burn = "BURN";       // DecreaseLiquidity
    public const string Collect = "COLLECT"; // Collect
}

/// <summary>
/// A raw NonfungiblePositionManager event read from chain (ARCHITECTURE.md §5/§6), with native gas — the
/// thin output of <see cref="IChainEventReader"/>. It deliberately carries only what logs + receipts give:
/// raw integers are text (no token-decimal scaling), gas is native wei (no USD), and tick/pool ownership
/// are NOT resolved here. Enrichment (ownership via <c>Transfer</c>, <c>positions(tokenId)</c> for ticks,
/// <c>factory.getPool</c>, token decimals, gas→USD, building the persistable <c>PositionEvent</c>) is the
/// DataSync worker/Application's job — see OPEN-DECISIONS.md.
/// </summary>
public sealed record ChainPositionEvent(
    string ChainId,
    string TxHash,
    int LogIndex,
    long BlockNumber,
    DateTimeOffset BlockTimeUtc,
    string TokenId,
    string EventType,
    string LiquidityDeltaRaw,
    string Amount0Raw,
    string Amount1Raw,
    string GasUsed,
    string? EffectiveGasPriceWei,
    string? NativeGasCostWei,
    string? Recipient,
    string PositionManagerAddress);
