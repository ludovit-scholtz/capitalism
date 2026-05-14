using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MasterApi.Tests;

public sealed class PersonalAccountNamePropagationServiceTests
{
    private static readonly string SuccessfulResponseBody = JsonSerializer.Serialize(new
    {
        data = new
        {
            syncPersonalAccountNameFromMaster = new
            {
                playerFound = true,
                wasUpdated = true,
                personalAccountName = "Nova Ledger",
                gender = "FEMALE",
            },
        },
    });

    private static MasterDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase($"masterapi-personal-account-propagation-{Guid.NewGuid():N}")
            .Options;
        return new MasterDbContext(options);
    }

    private static PersonalAccountNamePropagationService CreateService(
        MasterDbContext db,
        RecordingHandler handler,
        MasterServerOptions? options = null)
    {
        return new PersonalAccountNamePropagationService(
            db,
            new HttpClient(handler),
            Options.Create(options ?? new MasterServerOptions
            {
                RegistrationKey = "master-registration-key",
                ActiveThresholdSeconds = 90,
            }),
            NullLogger<PersonalAccountNamePropagationService>.Instance);
    }

    private static GameServerNode CreateServer(
        string serverKey,
        string graphqlUrl,
        bool isActive = true,
        DateTime? expiresAtUtc = null)
    {
        var now = DateTime.UtcNow;
        return new GameServerNode
        {
            Id = Guid.NewGuid(),
            ServerKey = serverKey,
            ServerKeyHash = $"hash-{serverKey}",
            IsActive = isActive,
            ExpiresAtUtc = expiresAtUtc ?? now.AddMinutes(5),
            DisplayName = serverKey,
            Description = $"Shard {serverKey}",
            Region = "eu",
            Environment = "production",
            BackendUrl = graphqlUrl.Replace("/graphql", string.Empty, StringComparison.Ordinal),
            GraphqlUrl = graphqlUrl,
            FrontendUrl = graphqlUrl.Replace("/graphql", "/app", StringComparison.Ordinal),
            Version = "1.0.0",
            PlayerCount = 0,
            CompanyCount = 0,
            CurrentTick = 0,
            RegisteredAtUtc = now,
            LastHeartbeatAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    [Fact]
    public async Task PropagateAsync_ActiveShards_PostsAliasToEachActiveServer()
    {
        await using var db = CreateDbContext();
        db.GameServers.AddRange(
            CreateServer("capitalism-eu", "https://capitalism-eu.example.com/graphql"),
            CreateServer("capitalism-us", "https://capitalism-us.example.com/graphql", isActive: false),
            CreateServer(
                "capitalism-archive",
                "https://capitalism-archive.example.com/graphql",
                expiresAtUtc: DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var handler = new RecordingHandler();
        var service = CreateService(db, handler);

        await service.PropagateAsync("player@example.com", "Nova Ledger", "FEMALE", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://capitalism-eu.example.com/graphql", request.RequestUri?.ToString());

        using var document = JsonDocument.Parse(request.Body);
        var input = document.RootElement.GetProperty("variables").GetProperty("input");
        Assert.Equal("master-registration-key", input.GetProperty("registrationKey").GetString());
        Assert.Equal("capitalism-eu", input.GetProperty("serverKey").GetString());
        Assert.Equal("player@example.com", input.GetProperty("playerEmail").GetString());
        Assert.Equal("Nova Ledger", input.GetProperty("personalAccountName").GetString());
        Assert.Equal("FEMALE", input.GetProperty("gender").GetString());
    }

    [Fact]
    public async Task PropagateAsync_WhenOneShardFails_StillAttemptsRemainingServers()
    {
        await using var db = CreateDbContext();
        db.GameServers.AddRange(
            CreateServer("capitalism-eu", "https://capitalism-eu.example.com/graphql"),
            CreateServer("capitalism-us", "https://capitalism-us.example.com/graphql"));
        await db.SaveChangesAsync();

        var handler = new RecordingHandler
        {
            ResponseFactory = request =>
            {
                var isFailingShard = string.Equals(request.RequestUri?.Host, "capitalism-us.example.com", StringComparison.Ordinal);
                return isFailingShard
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("Server error", Encoding.UTF8, "text/plain"),
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(SuccessfulResponseBody, Encoding.UTF8, "application/json"),
                    };
            },
        };
        var service = CreateService(db, handler);

        await service.PropagateAsync("player@example.com", "Nova Ledger", "FEMALE", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(handler.Requests, request => request.RequestUri?.Host == "capitalism-eu.example.com");
        Assert.Contains(handler.Requests, request => request.RequestUri?.Host == "capitalism-us.example.com");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ConcurrentBag<RecordedRequest> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } = _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessfulResponseBody, Encoding.UTF8, "application/json"),
            };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.RequestUri, body));
            return ResponseFactory(request);
        }
    }

    private sealed record RecordedRequest(Uri? RequestUri, string Body);
}
