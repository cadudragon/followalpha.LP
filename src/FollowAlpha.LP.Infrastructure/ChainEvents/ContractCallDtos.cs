using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

// Read-only contract-call DTOs used to enrich raw position events (ARCHITECTURE.md §6). Nethereum encodes
// these for eth_call and deserializes the result — exercised offline by the transport-level RecordedRpcClient.

/// <summary>NonfungiblePositionManager <c>positions(uint256)</c>.</summary>
[Function("positions", typeof(PositionsOutputDto))]
public sealed class PositionsFunction : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)]
    public BigInteger TokenId { get; set; }
}

/// <summary>The <c>positions</c> tuple (only the fields the enricher needs are consumed downstream).</summary>
[FunctionOutput]
public sealed class PositionsOutputDto : IFunctionOutputDTO
{
    [Parameter("uint96", "nonce", 1)]
    public BigInteger Nonce { get; set; }

    [Parameter("address", "operator", 2)]
    public string Operator { get; set; } = string.Empty;

    [Parameter("address", "token0", 3)]
    public string Token0 { get; set; } = string.Empty;

    [Parameter("address", "token1", 4)]
    public string Token1 { get; set; } = string.Empty;

    [Parameter("uint24", "fee", 5)]
    public uint Fee { get; set; }

    [Parameter("int24", "tickLower", 6)]
    public int TickLower { get; set; }

    [Parameter("int24", "tickUpper", 7)]
    public int TickUpper { get; set; }

    [Parameter("uint128", "liquidity", 8)]
    public BigInteger Liquidity { get; set; }

    [Parameter("uint256", "feeGrowthInside0LastX128", 9)]
    public BigInteger FeeGrowthInside0LastX128 { get; set; }

    [Parameter("uint256", "feeGrowthInside1LastX128", 10)]
    public BigInteger FeeGrowthInside1LastX128 { get; set; }

    [Parameter("uint128", "tokensOwed0", 11)]
    public BigInteger TokensOwed0 { get; set; }

    [Parameter("uint128", "tokensOwed1", 12)]
    public BigInteger TokensOwed1 { get; set; }
}

/// <summary>Uniswap v3 factory <c>getPool(address, address, uint24)</c> → pool address.</summary>
[Function("getPool", "address")]
public sealed class GetPoolFunction : FunctionMessage
{
    [Parameter("address", "tokenA", 1)]
    public string TokenA { get; set; } = string.Empty;

    [Parameter("address", "tokenB", 2)]
    public string TokenB { get; set; } = string.Empty;

    [Parameter("uint24", "fee", 3)]
    public uint Fee { get; set; }
}

/// <summary>ERC-20 <c>decimals()</c> → uint8.</summary>
[Function("decimals", "uint8")]
public sealed class DecimalsFunction : FunctionMessage
{
}
