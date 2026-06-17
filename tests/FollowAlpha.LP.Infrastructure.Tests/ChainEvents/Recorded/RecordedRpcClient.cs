using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents.Recorded;

/// <summary>
/// A Nethereum transport-level client that replays recorded JSON-RPC responses (keyed by
/// <see cref="RpcCallKey"/>) — no network, no key. Driving the real <c>NethereumEvmRpc</c> over this proves
/// the production request building and decode against real captured wire data (OPEN-DECISIONS.md). A cache
/// miss throws with the requested key so a new capture is obvious.
/// </summary>
internal sealed class RecordedRpcClient(JObject fixture) : ClientBase
{
    public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null!)
    {
        var key = RpcCallKey.For(request);
        var result = fixture[key];
        if (result is null)
        {
            throw new KeyNotFoundException($"No recorded RPC response for key: {key}");
        }

        return Task.FromResult(new RpcResponseMessage(request.Id, result));
    }

    protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
        throw new NotSupportedException("Batch requests are not used by the readers.");
}
