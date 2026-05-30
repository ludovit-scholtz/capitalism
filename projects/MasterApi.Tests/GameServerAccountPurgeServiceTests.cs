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

public sealed class GameServerAccountPurgeServiceTests
{
    private static readonly string SuccessfulResponseBody = JsonSerializer.Serialize(new
    {
        data = new
        {
            purgePlayerAccountFromMaster = new
            {
                playerFound = true,
                companiesRemoved = 1,
                buildingsDestroyed = 2,
                banksTransferredToGovernment = 1,
            },
        },
    });

    private static MasterDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase($"masterapi-account-purge-{Guid.NewGuid():N}")
            .Options;
        return new MasterDbContext(options);
    }

    private static GameServerAccountPurgeService CreateService(MasterDbContext db, RecordingHandler handler)
    {
        return new GameServerAccountPurgeService(
            db,
            new HttpClient(handler),
            Options.Create(new MasterServerOptions
            {
                RegistrationKey = "master-registration-key",
                ActiveThresholdSeconds = 90,
            }),
            NullLogger<GameServerAccountPurgeService>.Instance);
    }

    private static GameServerNode CreateServer(string serverKey, string graphqlUrl)
    {
        var now = DateTime.UtcNow;
        return new GameServerNode
        {
            Id = Guid.NewGuid(),
            ServerKey = serverKey,
            ServerKeyHash = $"hash-{serverKey}",
            IsActive = true,
            ExpiresAtUtc = now.AddMinutes(5),
            DisplayName = serverKey,
            Description = $"Shard {serverKey}",
            Region = "eu",
            Environment = "production",
            BackendUrl = graphqlUrl.Replace("/graphql", string.Empty, StringComparison.Ordinal),
            GraphqlUrl = graphqlUrl,
            FrontendUrl = graphqlUrl.Replace("/graphql", "/app", StringComparison.Ordinal),
            Version = "1.0.0",
            RegisteredAtUtc = now,
            LastHeartbeatAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    [Fact]
    public async Task PurgeAsync_PostsPurgeMutationToActiveServers()
    {
        await using var db = CreateDbContext();
        db.GameServers.Add(CreateServer("capitalism-eu", "https://capitalism-eu.example.com/graphql"));
        await db.SaveChangesAsync();

        var handler = new RecordingHandler();
        var service = CreateService(db, handler);

        await service.PurgeAsync("player@example.com", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        using var document = JsonDocument.Parse(request.Body);
        var input = document.RootElement.GetProperty("variables").GetProperty("input");
        Assert.Equal("master-registration-key", input.GetProperty("registrationKey").GetString());
        Assert.Equal("capitalism-eu", input.GetProperty("serverKey").GetString());
        Assert.Equal("player@example.com", input.GetProperty("playerEmail").GetString());
    }

    [Fact]
    public async Task PurgeAsync_WhenServerFails_ThrowsGameServerPurgeException()
    {
        await using var db = CreateDbContext();
        db.GameServers.Add(CreateServer("capitalism-eu", "https://capitalism-eu.example.com/graphql"));
        await db.SaveChangesAsync();

        var handler = new RecordingHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server error", Encoding.UTF8, "text/plain"),
            },
        };
        var service = CreateService(db, handler);

        await Assert.ThrowsAsync<GameServerPurgeException>(
            () => service.PurgeAsync("player@example.com", CancellationToken.None));
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
