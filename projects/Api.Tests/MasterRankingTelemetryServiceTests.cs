using System.Net;
using System.Text;
using Api.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class MasterRankingTelemetryServiceTests
{
    [Fact]
    public async Task ReportEventAsync_ConcurrentSameScopeAcrossServiceInstances_SendsSingleHttpRequest()
    {
        var handler = new CountingHttpHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(50, cancellationToken);
            return CreateOkResponse();
        });
        var options = CreateOptions();
        var firstService = CreateService(handler, options);
        var secondService = CreateService(handler, options);

        await Task.WhenAll(
            firstService.ReportEventAsync(MasterRankingBountyCodes.Manufacturer, "dup@example.com", uniqueScopeKey: "manufacturer:dup@example.com:20260512:capitalism-eu-1"),
            secondService.ReportEventAsync(MasterRankingBountyCodes.Manufacturer, "dup@example.com", uniqueScopeKey: "manufacturer:dup@example.com:20260512:capitalism-eu-1"));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReportEventAsync_DifferentScopeKeys_SendSeparateHttpRequests()
    {
        var handler = new CountingHttpHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            return CreateOkResponse();
        });
        var service = CreateService(handler, CreateOptions());

        await Task.WhenAll(
            service.ReportEventAsync(MasterRankingBountyCodes.Manufacturer, "player@example.com", uniqueScopeKey: "manufacturer:player@example.com:20260512:capitalism-eu-1"),
            service.ReportEventAsync(MasterRankingBountyCodes.Manufacturer, "player@example.com", uniqueScopeKey: "manufacturer:player@example.com:20260513:capitalism-eu-1"));

        Assert.Equal(2, handler.RequestCount);
    }

    private static MasterRankingTelemetryService CreateService(
        HttpMessageHandler handler,
        IOptions<MasterServerRegistrationOptions> options)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://master.example/")
        };

        return new MasterRankingTelemetryService(
            new StubHttpClientFactory(httpClient),
            options,
            NullLogger<MasterRankingTelemetryService>.Instance);
    }

    private static IOptions<MasterServerRegistrationOptions> CreateOptions()
    {
        return Options.Create(new MasterServerRegistrationOptions
        {
            ApiUrl = "https://master.example/graphql",
            RegistrationKey = "test-registration-key",
            ServerKey = "capitalism-eu-1",
            TelemetryEnabled = true,
        });
    }

    private static HttpResponseMessage CreateOkResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"ingestRankingEvent\":{\"id\":\"evt-1\",\"status\":\"PENDING\"}}}",
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return await responseFactory(request, cancellationToken);
        }
    }
}