using System.Net;
using System.Text;

namespace FollowAlpha.LP.Infrastructure.Tests.TheGraph;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that returns queued response bodies in order and records the
/// requests it saw — so adapter tests run entirely offline against recorded fixtures.
/// </summary>
internal sealed class StubHttpMessageHandler(params string[] responseBodies) : HttpMessageHandler
{
    private readonly Queue<string> _responses = new(responseBodies);

    public List<Uri?> RequestUris { get; } = [];

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri);
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No more stubbed responses.");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json"),
        };
    }
}
