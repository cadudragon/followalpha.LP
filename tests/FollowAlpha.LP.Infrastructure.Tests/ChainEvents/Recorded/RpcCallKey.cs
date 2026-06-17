using System.Globalization;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents.Recorded;

/// <summary>
/// A stable key for a JSON-RPC request, computed identically by the recorder and the replayer so a captured
/// response can be matched offline. Discriminates the methods the readers use: <c>eth_call</c> by
/// (to, data), <c>eth_getLogs</c> by its topic filter, receipt by tx hash, block by number. The block
/// range of <c>eth_getLogs</c> is deliberately not part of the key (each topic set is queried once).
/// </summary>
internal static class RpcCallKey
{
    public static string For(RpcRequestMessage request)
    {
        var p = (object[])request.RawParameters;
        return request.Method switch
        {
            "eth_blockNumber" => "eth_blockNumber",
            "eth_getTransactionReceipt" => $"eth_getTransactionReceipt:{p[0]}",
            "eth_getBlockByNumber" => $"eth_getBlockByNumber:{BlockKey(p[0])}",
            "eth_call" => $"eth_call:{CallKey(p[0])}",
            "eth_getLogs" => $"eth_getLogs:{LogsKey(p[0])}",
            _ => $"{request.Method}:{JsonConvert.SerializeObject(p)}",
        };
    }

    private static string CallKey(object callInput)
    {
        var input = (CallInput)callInput;
        return $"{input.To?.ToLowerInvariant()}:{input.Data?.ToLowerInvariant()}";
    }

    private static string LogsKey(object filter) =>
        JsonConvert.SerializeObject(((NewFilterInput)filter).Topics);

    private static string BlockKey(object blockParameter)
    {
        var bp = (BlockParameter)blockParameter;
        return bp.ParameterType == BlockParameter.BlockParameterType.blockNumber
            ? bp.BlockNumber.Value.ToString(CultureInfo.InvariantCulture)
            : bp.ParameterType.ToString();
    }
}
