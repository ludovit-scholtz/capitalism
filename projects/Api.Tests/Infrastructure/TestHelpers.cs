using Api.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared test helpers for backend integration tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Creates an <see cref="NbsExchangeRateService"/> that always uses fallback rates
    /// (no real HTTP calls) so tests are deterministic and do not depend on external services.
    /// </summary>
    public static NbsExchangeRateService CreateFallbackNbsService() =>
        new(new AlwaysFailingHttpClientFactory(), NullLogger<NbsExchangeRateService>.Instance);

    /// <summary>
    /// Creates an <see cref="NbsExchangeRateService"/> that returns the specified CSV content
    /// as if it had been fetched from the NBS live feed, without making a real HTTP call.
    /// Use this to unit-test CSV parsing logic (valid, malformed, empty content).
    /// </summary>
    public static NbsExchangeRateService CreateCsvParsingService(string csvContent) =>
        new(new StaticCsvHttpClientFactory(csvContent), NullLogger<NbsExchangeRateService>.Instance);

    /// <summary>
    /// A minimal <see cref="IHttpClientFactory"/> that always throws, triggering
    /// the NBS service's fallback-rates path.
    /// </summary>
    private sealed class AlwaysFailingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var handler = new AlwaysFailingHandler();
            return new HttpClient(handler);
        }

        private sealed class AlwaysFailingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw new HttpRequestException("Test stub: HTTP requests are disabled in integration tests.");
        }
    }

    /// <summary>
    /// An <see cref="IHttpClientFactory"/> that returns a fixed CSV string for any URL,
    /// simulating the NBS live feed with controlled content.
    /// </summary>
    private sealed class StaticCsvHttpClientFactory(string csvContent) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var handler = new StaticResponseHandler(csvContent);
            return new HttpClient(handler);
        }

        private sealed class StaticResponseHandler(string content) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                };
                return Task.FromResult(response);
            }
        }
    }
}
