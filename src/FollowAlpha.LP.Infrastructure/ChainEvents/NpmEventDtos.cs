using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

// Nethereum event DTOs for the Uniswap v3 NonfungiblePositionManager. Public so Nethereum's ABI
// reflection can read the [Event]/[Parameter] attributes when decoding logs.

/// <summary><c>IncreaseLiquidity(uint256 indexed tokenId, uint128 liquidity, uint256 amount0, uint256 amount1)</c> → MINT.</summary>
[Event("IncreaseLiquidity")]
public sealed class IncreaseLiquidityEventDto : IEventDTO
{
    [Parameter("uint256", "tokenId", 1, true)] public BigInteger TokenId { get; set; }

    [Parameter("uint128", "liquidity", 2, false)] public BigInteger Liquidity { get; set; }

    [Parameter("uint256", "amount0", 3, false)] public BigInteger Amount0 { get; set; }

    [Parameter("uint256", "amount1", 4, false)] public BigInteger Amount1 { get; set; }
}

/// <summary><c>DecreaseLiquidity(uint256 indexed tokenId, uint128 liquidity, uint256 amount0, uint256 amount1)</c> → BURN.</summary>
[Event("DecreaseLiquidity")]
public sealed class DecreaseLiquidityEventDto : IEventDTO
{
    [Parameter("uint256", "tokenId", 1, true)] public BigInteger TokenId { get; set; }

    [Parameter("uint128", "liquidity", 2, false)] public BigInteger Liquidity { get; set; }

    [Parameter("uint256", "amount0", 3, false)] public BigInteger Amount0 { get; set; }

    [Parameter("uint256", "amount1", 4, false)] public BigInteger Amount1 { get; set; }
}

/// <summary><c>Collect(uint256 indexed tokenId, address recipient, uint256 amount0, uint256 amount1)</c> → COLLECT.</summary>
[Event("Collect")]
public sealed class CollectEventDto : IEventDTO
{
    [Parameter("uint256", "tokenId", 1, true)] public BigInteger TokenId { get; set; }

    [Parameter("address", "recipient", 2, false)] public string Recipient { get; set; } = string.Empty;

    [Parameter("uint256", "amount0", 3, false)] public BigInteger Amount0 { get; set; }

    [Parameter("uint256", "amount1", 4, false)] public BigInteger Amount1 { get; set; }
}
