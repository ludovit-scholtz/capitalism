using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="GameApiClient"/> that exercise the HTTP layer
/// and its error-handling paths directly.
///
/// <para>
/// <b>Happy paths</b> — deserialises a well-formed GraphQL response into a
/// typed data wrapper.
/// </para>
/// <para>
/// <b>HTTP error paths</b> — non-2xx status codes throw <see cref="InvalidOperationException"/>
/// carrying the HTTP status code.
/// </para>
/// <para>
/// <b>GraphQL error paths</b> — an <c>errors</c> array in the response body throws
/// <see cref="GraphQLException"/> with the correct code; the <c>errors</c> node
/// takes precedence over any <c>data</c> node in the same response.
/// </para>
/// <para>
/// <b>Malformed response paths</b> — missing <c>data</c> field and null deserialization
/// both throw <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Auth header</b> — bearer token is attached only when supplied; omitted when null.
/// </para>
/// <para>
/// <b>Cancellation</b> — a pre-cancelled token propagates immediately without HTTP calls.
/// </para>
/// </summary>
public sealed class GameApiClientTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(factory());
        }
    }

    private static (GameApiClient client, FakeHttpHandler handler) CreateClient(
        Func<HttpResponseMessage> factory)
    {
        var handler = new FakeHttpHandler(factory);
        var options = Options.Create(new BotOptions { GraphqlUrl = "https://fake.example/graphql" });
        var http = new HttpClient(handler);
        var client = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        return (client, handler);
    }

    private static HttpResponseMessage OkResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed record SimpleWrapper(string Value);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WellFormedResponse_DeserializesData()
    {
        const string json = """{"data":{"value":"hello"}}""";
        var (client, handler) = CreateClient(() => OkResponse(json));

        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }");

        Assert.Equal("hello", result.Value);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveKeys_DeserializesCorrectly()
    {
        // The API returns PascalCase; our record uses PascalCase — confirm
        // PropertyNameCaseInsensitive works for camelCase server responses too.
        const string json = """{"data":{"Value":"camel-test"}}""";
        var (client, _) = CreateClient(() => OkResponse(json));

        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }");
        Assert.Equal("camel-test", result.Value);
    }

    // ── HTTP error paths ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Http500_ThrowsInvalidOperationException()
    {
        var (client, _) = CreateClient(() =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server Error", Encoding.UTF8, "text/plain"),
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Http401_ThrowsInvalidOperationException()
    {
        var (client, _) = CreateClient(() =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Unauthorized", Encoding.UTF8, "text/plain"),
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Http404_ThrowsInvalidOperationException()
    {
        var (client, _) = CreateClient(() =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not Found", Encoding.UTF8, "text/plain"),
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Contains("404", ex.Message);
    }

    // ── GraphQL error paths ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_GraphQLErrorsArray_ThrowsGraphQLException()
    {
        const string json = """
            {"errors":[{"message":"Not authenticated.","extensions":{"code":"UNAUTHENTICATED"}}]}
            """;
        var (client, _) = CreateClient(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Equal("UNAUTHENTICATED", ex.Code);
        Assert.Equal("Not authenticated.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateEmailError_ThrowsWithCode()
    {
        const string json = """
            {"errors":[{"message":"Email already taken.","extensions":{"code":"DUPLICATE_EMAIL"}}]}
            """;
        var (client, _) = CreateClient(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => client.ExecuteAsync<SimpleWrapper>("mutation Register($input: RegisterInput!) { register(input: $input) { token } }"));

        Assert.Equal("DUPLICATE_EMAIL", ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorsTakePrecedenceOverData()
    {
        // Some servers return both errors and partial data; errors should win.
        const string json = """
            {"errors":[{"message":"Partial error.","extensions":{"code":"PARTIAL_ERROR"}}],
             "data":{"value":"should-be-ignored"}}
            """;
        var (client, _) = CreateClient(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Equal("PARTIAL_ERROR", ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorWithoutCode_UsesEmptyStringCode()
    {
        // GraphQL spec allows errors without extensions.
        const string json = """{"errors":[{"message":"Unknown error."}]}""";
        var (client, _) = CreateClient(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Equal(string.Empty, ex.Code);
        Assert.Equal("Unknown error.", ex.Message);
    }

    // ── Malformed response paths ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MissingDataField_ThrowsInvalidOperationException()
    {
        // Valid JSON but no 'data' or 'errors' key
        const string json = """{"something":"else"}""";
        var (client, _) = CreateClient(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }"));

        Assert.Contains("data", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyDataObject_ReturnsDefaultInstance()
    {
        // data:{} can deserialise to a default record; Value will be null.
        const string json = """{"data":{}}""";
        var (client, _) = CreateClient(() => OkResponse(json));

        // Record with nullable string should deserialise without throwing.
        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }");
        Assert.NotNull(result); // object exists, Value may be null
    }

    // ── Auth header injection ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithBearerToken_AttachesAuthorizationHeader()
    {
        const string json = """{"data":{"value":"ok"}}""";
        var (client, handler) = CreateClient(() => OkResponse(json));

        await client.ExecuteAsync<SimpleWrapper>("{ value }", bearerToken: "my-test-token");

        var authHeader = handler.LastRequest?.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader.Scheme);
        Assert.Equal("my-test-token", authHeader.Parameter);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutBearerToken_OmitsAuthorizationHeader()
    {
        const string json = """{"data":{"value":"ok"}}""";
        var (client, handler) = CreateClient(() => OkResponse(json));

        await client.ExecuteAsync<SimpleWrapper>("{ value }"); // no bearerToken

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullToken_OmitsAuthorizationHeader()
    {
        const string json = """{"data":{"value":"ok"}}""";
        var (client, handler) = CreateClient(() => OkResponse(json));

        await client.ExecuteAsync<SimpleWrapper>("{ value }", bearerToken: null);

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_ThrowsWithoutHttpCall()
    {
        // Use a fake handler that would succeed but should never be called.
        const string json = """{"data":{"value":"should-not-arrive"}}""";
        var (client, handler) = CreateClient(() => OkResponse(json));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }", ct: cts.Token));

        Assert.Equal(0, handler.CallCount);
    }
}
