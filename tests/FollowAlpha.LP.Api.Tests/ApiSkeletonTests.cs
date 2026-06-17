using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FollowAlpha.LP.Api.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FollowAlpha.LP.Api.Tests;

/// <summary>
/// Phase 3.1 foundation tests: the host boots in-memory, /health answers, the X-Api-Key gate accepts/rejects
/// per API-CONTRACT §1, errors are RFC 7807 problem+json, the 422 insufficient-data path works (§2), and the
/// OpenAPI document is exposed. No product behaviour is asserted — there is none yet.
/// </summary>
public sealed class ApiSkeletonTests : IClassFixture<ApiSkeletonTests.Factory>
{
    private const string ValidKey = "test-key";
    private readonly Factory _factory;

    public ApiSkeletonTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Testing environment so the dev/test-only diagnostic probes are mapped; key via in-memory config.
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(c =>
                c.AddInMemoryCollection(new Dictionary<string, string?> { ["LP_API_KEY"] = ValidKey }));
            return base.CreateHost(builder);
        }
    }

    private HttpClient Client() => _factory.CreateClient();

    [Fact]
    public async Task Health_is_anonymous_and_returns_ok()
    {
        var response = await Client().GetAsync("/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Secured_endpoint_without_api_key_is_401_problem_json()
    {
        var response = await Client().GetAsync("/v1/_diagnostics/ping");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Secured_endpoint_with_wrong_api_key_is_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/_diagnostics/ping");
        request.Headers.Add(ApiKeyEndpointFilter.HeaderName, "not-the-key");

        var response = await Client().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Secured_endpoint_with_correct_api_key_is_accepted()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/_diagnostics/ping");
        request.Headers.Add(ApiKeyEndpointFilter.HeaderName, ValidKey);

        var response = await Client().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pong").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Insufficient_data_path_returns_422_problem_json_with_missing()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/_diagnostics/insufficient-data");
        request.Headers.Add(ApiKeyEndpointFilter.HeaderName, ValidKey);

        var response = await Client().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(422);
        problem.GetProperty("title").GetString().Should().Be("Insufficient data");
        var missing = problem.GetProperty("missing").EnumerateArray().Select(e => e.GetString());
        missing.Should().Contain("priceBars");
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var response = await Client().GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"openapi\"");
    }

    [Theory]
    [InlineData("/v1/_diagnostics/ping")]
    [InlineData("/v1/_diagnostics/insufficient-data")]
    public async Task Diagnostic_probes_are_marked_deprecated_in_openapi(string path)
    {
        var doc = await Client().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var get = doc.GetProperty("paths").GetProperty(path).GetProperty("get");
        get.GetProperty("deprecated").GetBoolean().Should().BeTrue();
    }
}
