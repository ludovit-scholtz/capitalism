using System.Text;
using System.Text.Json;
using Api.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared test helpers for backend integration tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Executes a GraphQL request against the test HTTP client and returns the
    /// deserialised <see cref="JsonElement"/> response body.
    /// Can be used by any test class that holds an <see cref="HttpClient"/> from a
    /// <see cref="ApiWebApplicationFactory"/> without duplicating the helper.
    /// </summary>
    public static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    /// <summary>
    /// Creates an <see cref="NbsExchangeRateService"/> that always uses fallback rates
    /// (no real HTTP calls) so tests are deterministic and do not depend on external services.
    /// </summary>
    public static NbsExchangeRateService CreateFallbackNbsService() =>
        new(new AlwaysFailingHttpClientFactory(), new TestEnvironment(), NullLogger<NbsExchangeRateService>.Instance);

    /// <summary>
    /// Creates an <see cref="NbsExchangeRateService"/> that returns the specified CSV content
    /// as if it had been fetched from the NBS live feed, without making a real HTTP call.
    /// Use this to unit-test CSV parsing logic (valid, malformed, empty content).
    /// </summary>
    public static NbsExchangeRateService CreateCsvParsingService(string csvContent) =>
        new(new StaticCsvHttpClientFactory(csvContent), new TestEnvironment("Development"), NullLogger<NbsExchangeRateService>.Instance);

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string environmentName = "Testing")
        {
            EnvironmentName = environmentName;
        }

        public string ApplicationName { get; set; } = "Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

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
