using System.Text;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents.Recorded;

/// <summary>
/// A Nethereum transport-level client that POSTs to a real public RPC and records each response's result
/// (keyed by <see cref="RpcCallKey"/>) so the offline replay fixture can be regenerated. Used only by the
/// opt-in, env-gated smoke/capture test — never on the default offline path. Serializes the request with
/// Nethereum's own settings so the wire body matches the production client.
/// </summary>
internal sealed class RecordingRpcClient(string url) : ClientBase, IDisposable
{
    private static readonly JsonSerializerSettings Settings =
        DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

    private readonly HttpClient _http = new();

    public JObject Recorded { get; } = new();

    public void Dispose() => _http.Dispose();

    public override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null!)
    {
        var body = JsonConvert.SerializeObject(request, Settings);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content);
        var text = await response.Content.ReadAsStringAsync();

        var message = JsonConvert.DeserializeObject<RpcResponseMessage>(text, Settings)!;
        if (message.Result is not null)
        {
            // Result is typed `object`: complex results deserialize to JToken, primitives (eth_call /
            // eth_blockNumber hex strings) to .NET strings. Normalize both to a JToken for the fixture.
            Recorded[RpcCallKey.For(request)] = message.Result as JToken ?? JToken.FromObject(message.Result);
        }

        return message;
    }

    protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
        throw new NotSupportedException("Batch requests are not used by the readers.");
}
