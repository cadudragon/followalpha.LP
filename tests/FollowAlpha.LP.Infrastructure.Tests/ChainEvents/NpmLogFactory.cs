using System.Globalization;
using System.Numerics;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents;

/// <summary>
/// Builds ABI-correct NonfungiblePositionManager <see cref="FilterLog"/>s (real keccak topic0, indexed
/// tokenId topic1, ABI-encoded data) so the reader's real Nethereum log→DTO decode is exercised offline.
/// These are representative captures; chain-recorded JSON-RPC captures remain ideal (OPEN-DECISIONS.md).
/// </summary>
internal static class NpmLogFactory
{
    public static string Topic<TEventDto>()
        where TEventDto : class, IEventDTO, new() =>
        "0x" + ABITypedRegistry.GetEvent<TEventDto>().Sha3Signature;

    public static FilterLog Increase(BigInteger tokenId, BigInteger liquidity, BigInteger amount0, BigInteger amount1, string txHash, int logIndex, long blockNumber) =>
        Build(
            Topic<IncreaseLiquidityEventDto>(), tokenId,
            Encode(new ABIValue("uint128", liquidity), new ABIValue("uint256", amount0), new ABIValue("uint256", amount1)),
            txHash, logIndex, blockNumber);

    public static FilterLog Decrease(BigInteger tokenId, BigInteger liquidity, BigInteger amount0, BigInteger amount1, string txHash, int logIndex, long blockNumber) =>
        Build(
            Topic<DecreaseLiquidityEventDto>(), tokenId,
            Encode(new ABIValue("uint128", liquidity), new ABIValue("uint256", amount0), new ABIValue("uint256", amount1)),
            txHash, logIndex, blockNumber);

    public static FilterLog Collect(BigInteger tokenId, string recipient, BigInteger amount0, BigInteger amount1, string txHash, int logIndex, long blockNumber) =>
        Build(
            Topic<CollectEventDto>(), tokenId,
            Encode(new ABIValue("address", recipient), new ABIValue("uint256", amount0), new ABIValue("uint256", amount1)),
            txHash, logIndex, blockNumber);

    private static byte[] Encode(params ABIValue[] values) => new ABIEncode().GetABIEncoded(values);

    private static FilterLog Build(string topic0, BigInteger tokenId, byte[] data, string txHash, int logIndex, long blockNumber) =>
        new()
        {
            Address = "0xC36442b4a4522E871399CD717aBDD847Ab11FE88",
            Topics = [topic0, ToTopic(tokenId)],
            Data = "0x" + Convert.ToHexStringLower(data),
            TransactionHash = txHash,
            LogIndex = new HexBigInteger(logIndex),
            BlockNumber = new HexBigInteger(blockNumber),
        };

    private static string ToTopic(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[32];
        Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
        return "0x" + Convert.ToHexStringLower(padded);
    }
}
