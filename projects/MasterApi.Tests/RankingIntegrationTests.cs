using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Capitalism.Shared.Security;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Tests;

public sealed class RankingIntegrationTests
{
    private const string SharedJwtIssuer = "Capitalism";
    private const string SharedJwtAudience = "Capitalism";
    private const string SharedJwtSigningKey = "TestingOnlyStrongSigningKey0123456789ABCDEF!";

    [Fact]
    public async Task IngestRankingEvent_RequiresServerKey()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = string.Empty,
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = $"rank-noserver-{Guid.NewGuid():N}@example.com",
                    occurredAtUtc = DateTime.UtcNow,
                    uniqueScopeKey = $"noserver-{Guid.NewGuid():N}",
                    payloadJson = "{}",
                }
            });

        var errors = result.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("code").GetString() == "SERVER_KEY_REQUIRED");
    }

    [Fact]
    public async Task IngestRankingEvent_UnknownShardKey_ReturnsForbiddenAndWritesAudit()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = $"unknown-{Guid.NewGuid():N}",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = $"rank-unknown-{Guid.NewGuid():N}@example.com",
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"unknown-{Guid.NewGuid():N}",
                    payloadJson = "{\"value\":1}",
                }
            });

        var errors = result.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("code").GetString() == "UNKNOWN_SHARD_KEY");
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("httpStatus").GetInt32() == 403);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var audit = db.RankingTelemetryAuditLogs
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        Assert.NotNull(audit);
        Assert.Equal(RankingTelemetryAuditReason.UnknownShardKey, audit!.ReasonCode);
        Assert.True(audit.IsRejected);
    }

    [Fact]
    public async Task IngestRankingEvent_StaleShardKey_ReturnsStaleCodeAndWritesAudit()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var serverKey = $"stale-{Guid.NewGuid():N}";
        await GraphQlAsync(
            client,
            """
            mutation RegisterServer($input: RegisterGameServerInput!) {
              registerGameServer(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    displayName = "Stale Server",
                    description = "Stale key test",
                    region = "EU",
                    environment = "test",
                    backendUrl = "https://stale.example.com",
                    graphqlUrl = "https://stale.example.com/graphql",
                    frontendUrl = "https://stale.example.com/app",
                    version = "1.0.0",
                    playerCount = 0,
                    companyCount = 0,
                    currentTick = 0,
                }
            });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var server = db.GameServers.First(item => item.ServerKey == serverKey);
            server.IsActive = false;
            server.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var result = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = $"rank-stale-{Guid.NewGuid():N}@example.com",
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"stale-{Guid.NewGuid():N}",
                    payloadJson = "{}",
                }
            });

        var errors = result.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("code").GetString() == "STALE_SHARD_KEY");

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var audit = verifyDb.RankingTelemetryAuditLogs
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        Assert.NotNull(audit);
        Assert.Equal(RankingTelemetryAuditReason.StaleShardKey, audit!.ReasonCode);
    }

    [Fact]
    public async Task IngestRankingEvent_DuplicateSignature_IsRejectedAndAudited()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var serverKey = $"dup-{Guid.NewGuid():N}";
        await GraphQlAsync(
            client,
            """
            mutation RegisterServer($input: RegisterGameServerInput!) {
              registerGameServer(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    displayName = "Duplicate Server",
                    description = "Duplicate key test",
                    region = "EU",
                    environment = "test",
                    backendUrl = "https://dup.example.com",
                    graphqlUrl = "https://dup.example.com/graphql",
                    frontendUrl = "https://dup.example.com/app",
                    version = "1.0.0",
                    playerCount = 1,
                    companyCount = 1,
                    currentTick = 100,
                }
            });

        var nonce = $"nonce-{Guid.NewGuid():N}";
        var payloadJson = "{\"price\":42}";
        var email = $"rank-dup-{Guid.NewGuid():N}@example.com";

        var first = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = email,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = nonce,
                    payloadJson,
                }
            });
        Assert.False(first.TryGetProperty("errors", out _));

        var duplicate = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = email,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = nonce,
                    payloadJson,
                }
            });

        var errors = duplicate.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("code").GetString() == "DUPLICATE_EVENT_SIGNATURE");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.True(db.RankingTelemetryAuditLogs.Any(item => item.ReasonCode == RankingTelemetryAuditReason.DuplicateEventSignature));
    }

    [Fact]
    public async Task IngestRankingEvent_WithSameIdempotencyKey_ReturnsOriginalResponse()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-idempotency-{Guid.NewGuid():N}@example.com";
        var idempotencyKey = $"idem-{Guid.NewGuid():N}";

        var first = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status createdAtUtc }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"idem-first-{Guid.NewGuid():N}",
                    uniqueScopeKey = "idem-scope",
                    idempotencyKey,
                    payloadJson = "{\"netWorthUsd\":1000}",
                }
            });
        Assert.False(first.TryGetProperty("errors", out _));
        var firstPayload = first.GetProperty("data").GetProperty("ingestRankingEvent");
        var firstId = firstPayload.GetProperty("id").GetString();

        var second = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status createdAtUtc }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow.AddMinutes(5),
                    externalEventId = $"idem-second-{Guid.NewGuid():N}",
                    uniqueScopeKey = "idem-scope-2",
                    idempotencyKey,
                    payloadJson = "{\"netWorthUsd\":1500}",
                }
            });
        Assert.False(second.TryGetProperty("errors", out _));
        var secondPayload = second.GetProperty("data").GetProperty("ingestRankingEvent");
        Assert.Equal(firstId, secondPayload.GetProperty("id").GetString());
        Assert.Equal(firstPayload.GetProperty("createdAtUtc").GetString(), secondPayload.GetProperty("createdAtUtc").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var events = await db.MasterRankingEvents
            .Where(item => item.PlayerEmail == userEmail)
            .ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task SubmitRankingProofEvent_DuplicateProofReference_ReturnsConflict()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var proofReference = $"https://x.com/player/status/{Guid.NewGuid():N}";
        var (firstToken, _) = await RegisterAsync(client, $"rank-proof-first-{Guid.NewGuid():N}@example.com", "Proof First");
        var (secondToken, _) = await RegisterAsync(client, $"rank-proof-second-{Guid.NewGuid():N}@example.com", "Proof Second");

        var firstSubmit = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!, $idempotencyKey: String) {
              submitRankingProofEvent(
                bountyCode: $bountyCode
                proofReference: $proofReference
                idempotencyKey: $idempotencyKey
              ) {
                id
                status
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference, idempotencyKey = $"proof-{Guid.NewGuid():N}" },
            firstToken);
        Assert.False(firstSubmit.TryGetProperty("errors", out _));

        var duplicateSubmit = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!) {
              submitRankingProofEvent(
                bountyCode: $bountyCode
                proofReference: $proofReference
              ) {
                id
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference },
            secondToken);

        var errors = duplicateSubmit.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("code").GetString() == "PROOF_REFERENCE_CONFLICT");
        Assert.Contains(errors, error => error.GetProperty("extensions").GetProperty("httpStatus").GetInt32() == 409);
    }

    [Fact]
    public async Task IngestRankingEvent_NonMonotonicNetWorth_IsQuarantinedForModeration()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-monotonic-{Guid.NewGuid():N}@example.com";

        var first = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"mono-a-{Guid.NewGuid():N}",
                    payloadJson = "{\"netWorthUsd\":2000}",
                }
            });
        Assert.False(first.TryGetProperty("errors", out _));

        var second = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow.AddMinutes(1),
                    externalEventId = $"mono-b-{Guid.NewGuid():N}",
                    payloadJson = "{\"netWorthUsd\":1900}",
                }
            });
        Assert.False(second.TryGetProperty("errors", out _));
        Assert.Equal(
            RankingEventStatus.PendingModeration,
            second.GetProperty("data").GetProperty("ingestRankingEvent").GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.True(db.RankingTelemetryAuditLogs.Any(item => item.ReasonCode == RankingTelemetryAuditReason.NonMonotonicNetWorth));
        Assert.True(db.MasterRankingEvents.Any(item => item.PlayerEmail == userEmail && item.IsQuarantined));
    }

    [Fact]
    public async Task IngestRankingEvent_MismatchedShardKey_IsQuarantinedForModeration()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-shard-mismatch-{Guid.NewGuid():N}@example.com";
        await RegisterServerAsync(client, "rank-server-eu");
        await RegisterServerAsync(client, "rank-server-us");
        var sharedNonce = $"shared-nonce-{Guid.NewGuid():N}";

        await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "rank-server-eu",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = sharedNonce,
                    payloadJson = "{}",
                }
            });

        var second = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "rank-server-us",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow.AddMinutes(1),
                    externalEventId = sharedNonce,
                    payloadJson = "{}",
                }
            });
        Assert.False(second.TryGetProperty("errors", out _));
        Assert.Equal(
            RankingEventStatus.PendingModeration,
            second.GetProperty("data").GetProperty("ingestRankingEvent").GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.True(db.RankingTelemetryAuditLogs.Any(item => item.ReasonCode == RankingTelemetryAuditReason.MismatchedShardKey));
    }

    [Fact]
    public async Task IngestRankingEvent_BurstSubmissions_AreQuarantinedForModeration()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-burst-{Guid.NewGuid():N}@example.com";
        var occurredAtUtc = DateTime.UtcNow;

        for (var index = 0; index < 9; index++)
        {
            var result = await GraphQlAsync(
                client,
                """
                mutation Ingest($input: IngestRankingEventInput!) {
                  ingestRankingEvent(input: $input) { id status }
                }
                """,
                new
                {
                    input = new
                    {
                        registrationKey = "test-registration-key",
                        serverKey = "capitalism-eu-1",
                        eventType = MasterRankingBountyCodes.FxTrader,
                        playerEmail = userEmail,
                        occurredAtUtc,
                        externalEventId = $"burst-{index}-{Guid.NewGuid():N}",
                        payloadJson = "{}",
                    }
                });

            Assert.False(result.TryGetProperty("errors", out _));
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.True(db.RankingTelemetryAuditLogs.Any(item => item.ReasonCode == RankingTelemetryAuditReason.BurstSubmissionPattern));
        Assert.True(db.MasterRankingEvents.Any(item => item.PlayerEmail == userEmail && item.Status == RankingEventStatus.PendingModeration));
    }

    [Fact]
    public async Task RankingTelemetryBatch_CanBeQuarantinedAndCleared_ByGlobalAdmin()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-quarantine-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Quarantine Player");
        var rootToken = CreateRootAdminToken();
        var serverKey = $"quarantine-{Guid.NewGuid():N}";

        await GraphQlAsync(
            client,
            """
            mutation RegisterServer($input: RegisterGameServerInput!) {
              registerGameServer(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    displayName = "Quarantine Server",
                    description = "Quarantine batch test",
                    region = "EU",
                    environment = "test",
                    backendUrl = "https://quarantine.example.com",
                    graphqlUrl = "https://quarantine.example.com/graphql",
                    frontendUrl = "https://quarantine.example.com/app",
                    version = "1.0.0",
                    playerCount = 1,
                    companyCount = 1,
                    currentTick = 1,
                }
            });

        await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"fx-{Guid.NewGuid():N}",
                    payloadJson = "{\"ok\":true}",
                }
            });

        var dashboard = await GraphQlAsync(
            client,
            """
            query {
              rankingAdminDashboard {
                flaggedTelemetryBatches {
                  batchId
                  serverKeyMasked
                  isQuarantined
                }
              }
            }
            """,
            token: rootToken);
        Assert.False(dashboard.TryGetProperty("errors", out _));

        var batches = dashboard.GetProperty("data").GetProperty("rankingAdminDashboard").GetProperty("flaggedTelemetryBatches").EnumerateArray().ToList();
        var maskedPrefix = serverKey[..4];
        var batch = batches.First(item =>
            item.GetProperty("serverKeyMasked").GetString()?.StartsWith(maskedPrefix, StringComparison.Ordinal) == true);
        var batchId = batch.GetProperty("batchId").GetString()!;

        var quarantined = await GraphQlAsync(
            client,
            """
            mutation Quarantine($batchId: UUID!, $reason: String!) {
              quarantineTelemetryBatch(batchId: $batchId, reason: $reason) {
                batchId
                isQuarantined
                quarantineReason
              }
            }
            """,
            new { batchId, reason = "Suspicious replay pattern" },
            token: rootToken);
        Assert.False(quarantined.TryGetProperty("errors", out _));
        Assert.True(quarantined.GetProperty("data").GetProperty("quarantineTelemetryBatch").GetProperty("isQuarantined").GetBoolean());

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);
        var summaryAfterQuarantine = await GraphQlAsync(
            client,
            "query { myRankingSummary { totalPoints } }",
            token: userToken);
        Assert.False(summaryAfterQuarantine.TryGetProperty("errors", out _));
        Assert.Equal(0m, summaryAfterQuarantine.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal());

        var cleared = await GraphQlAsync(
            client,
            """
            mutation Clear($batchId: UUID!, $justification: String!) {
              clearQuarantine(batchId: $batchId, justification: $justification) {
                batchId
                isQuarantined
                clearJustification
              }
            }
            """,
            new { batchId, justification = "Validated source integrity." },
            token: rootToken);
        Assert.False(cleared.TryGetProperty("errors", out _));
        Assert.False(cleared.GetProperty("data").GetProperty("clearQuarantine").GetProperty("isQuarantined").GetBoolean());

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);
        var summaryAfterClear = await GraphQlAsync(
            client,
            "query { myRankingSummary { totalPoints } }",
            token: userToken);
        Assert.False(summaryAfterClear.TryGetProperty("errors", out _));
        Assert.True(summaryAfterClear.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal() > 0m);
    }

    [Fact]
    public async Task IngestRankingEvent_HourlyEvaluation_IsIdempotentByUniquenessKey()
    {
      await using var factory = new MasterApiWebApplicationFactory();
      using var client = factory.CreateClient();

        var userEmail = $"rank-idempotent-{Guid.NewGuid():N}@example.com";
      var (userToken, _) = await RegisterAsync(client, userEmail, "Rank User");
        var rootToken = CreateRootAdminToken();

      var ingestResult = await GraphQlAsync(
        client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) {
                id
                eventType
                status
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"fx-{Guid.NewGuid():N}",
                    uniqueScopeKey = "swap-1",
                    payloadJson = "{}",
                }
            });

        Assert.False(ingestResult.TryGetProperty("errors", out _));

        var firstRun = await GraphQlAsync(
          client,
            """
            mutation {
              runRankingEvaluationNow {
                id
                status
                rewardRecordsCreated
              }
            }
            """,
            token: rootToken);

        Assert.False(firstRun.TryGetProperty("errors", out _));
        Assert.Equal("SUCCEEDED", firstRun.GetProperty("data").GetProperty("runRankingEvaluationNow").GetProperty("status").GetString());

        var secondRun = await GraphQlAsync(
          client,
            """
            mutation {
              runRankingEvaluationNow {
                id
                status
                rewardRecordsCreated
              }
            }
            """,
            token: rootToken);

        Assert.False(secondRun.TryGetProperty("errors", out _));

        var summary = await GraphQlAsync(
          client,
            """
            query {
              myRankingSummary {
                totalPoints
              }
            }
            """,
            token: userToken);

        Assert.False(summary.TryGetProperty("errors", out _));
        Assert.Equal(9m, summary.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal());

        var history = await GraphQlAsync(
          client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var historyItems = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        Assert.Single(historyItems, item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.FxTrader);
    }

    [Fact]
    public async Task TutorialBounty_OnceCooldown_AwardsOnlyOneRecordPerPlayer()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-tutorial-once-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Tutorial Once");
        var rootToken = CreateRootAdminToken();

        var ingestMutation = """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """;

        await GraphQlAsync(client, ingestMutation, new
        {
            input = new
            {
                registrationKey = "test-registration-key",
                serverKey = "capitalism-eu-1",
                eventType = MasterRankingBountyCodes.TutorialFirstGridEditorOpen,
                playerEmail = userEmail,
                occurredAtUtc = DateTime.UtcNow,
                uniqueScopeKey = "tutorial-visit-1",
                payloadJson = "{}",
            }
        });

        await GraphQlAsync(client, ingestMutation, new
        {
            input = new
            {
                registrationKey = "test-registration-key",
                serverKey = "capitalism-eu-1",
                eventType = MasterRankingBountyCodes.TutorialFirstGridEditorOpen,
                playerEmail = userEmail,
                occurredAtUtc = DateTime.UtcNow.AddMinutes(1),
                uniqueScopeKey = "tutorial-visit-2",
                payloadJson = "{}",
            }
        });

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id status } }", token: rootToken);

        var history = await GraphQlAsync(client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        Assert.Single(items, item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.TutorialFirstGridEditorOpen);
    }

    [Fact]
    public async Task TutorialBountyStatuses_ServiceQuery_OnlyReturnsAwardedForTargetPlayer()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-tutorial-status-{Guid.NewGuid():N}@example.com";
        var otherEmail = $"rank-tutorial-status-other-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, userEmail, "Tutorial Status");
        await RegisterAsync(client, otherEmail, "Tutorial Status Other");
        var rootToken = CreateRootAdminToken();

        await GraphQlAsync(client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.TutorialFirstBuildingDetailVisit,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    uniqueScopeKey = "tutorial-status-1",
                    payloadJson = "{}",
                }
            });

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id status } }", token: rootToken);

        var statusForTarget = await GraphQlAsync(client,
            """
            query Status($input: GetTutorialBountyStatusesInput!) {
              tutorialBountyStatuses(input: $input) {
                milestone
                isAwarded
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = string.Empty,
                    playerEmail = userEmail,
                }
            });

        var targetItems = statusForTarget.GetProperty("data").GetProperty("tutorialBountyStatuses").EnumerateArray().ToList();
        var targetMilestone = targetItems.First(item => item.GetProperty("milestone").GetString() == "FIRST_BUILDING_DETAIL_VISIT");
        Assert.True(targetMilestone.GetProperty("isAwarded").GetBoolean());

        var statusForOther = await GraphQlAsync(client,
            """
            query Status($input: GetTutorialBountyStatusesInput!) {
              tutorialBountyStatuses(input: $input) {
                milestone
                isAwarded
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = string.Empty,
                    playerEmail = otherEmail,
                }
            });

        var otherItems = statusForOther.GetProperty("data").GetProperty("tutorialBountyStatuses").EnumerateArray().ToList();
        var otherMilestone = otherItems.First(item => item.GetProperty("milestone").GetString() == "FIRST_BUILDING_DETAIL_VISIT");
        Assert.False(otherMilestone.GetProperty("isAwarded").GetBoolean());
    }

    [Fact]
    public async Task DailyDecay_Run_ReducesRankingPointsDeterministically()
    {
      await using var factory = new MasterApiWebApplicationFactory();
      using var client = factory.CreateClient();

        var userEmail = $"rank-decay-{Guid.NewGuid():N}@example.com";
      var (userToken, _) = await RegisterAsync(client, userEmail, "Decay User");
        var rootToken = CreateRootAdminToken();

      await GraphQlAsync(
        client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                  serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"fx-decay-{Guid.NewGuid():N}",
                    uniqueScopeKey = "swap-decay-1",
                    payloadJson = "{}",
                }
            });

              await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

              var beforeDecay = await GraphQlAsync(
                client,
            "query { myRankingSummary { totalPoints } }",
            token: userToken);
        var totalBefore = beforeDecay.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();
              Assert.True(totalBefore > 0m, "Expected pre-decay ranking points to be positive after evaluation.");

              var decayRun = await GraphQlAsync(
                client,
            "mutation { runRankingDailyDecayNow { status totalPointsBeforeDecay totalPointsAfterDecay } }",
            token: rootToken);

        Assert.False(decayRun.TryGetProperty("errors", out _));
        Assert.Equal("SUCCEEDED", decayRun.GetProperty("data").GetProperty("runRankingDailyDecayNow").GetProperty("status").GetString());

        var afterDecay = await GraphQlAsync(
          client,
            "query { myRankingSummary { totalPoints } }",
            token: userToken);
        var totalAfter = afterDecay.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();

        Assert.Equal(decimal.Round(totalBefore * 0.99m, 4, MidpointRounding.ToEven), totalAfter);
    }

    [Fact]
    public async Task SuggestionSupportTicket_TriggersGameImproverBounty_AfterEvaluation()
    {
      await using var factory = new MasterApiWebApplicationFactory();
      using var client = factory.CreateClient();

        var userEmail = $"rank-support-{Guid.NewGuid():N}@example.com";
      var (userToken, _) = await RegisterAsync(client, userEmail, "Support User");
        var rootToken = CreateRootAdminToken();

      var createSupport = await GraphQlAsync(
        client,
            """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) {
                id
                ticketType
              }
            }
            """,
            new
            {
                input = new
                {
                    ticketType = "SUGGESTION",
                    title = "Ranking quality suggestion",
                    markdownSource = "Please improve ranking telemetry and the readability of bounty history cards.",
                }
            },
            userToken);

        Assert.False(createSupport.TryGetProperty("errors", out _));

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var history = await GraphQlAsync(
          client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
                pointsAwarded
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        var gameImprover = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.GameImprover);
        Assert.Equal(JsonValueKind.Object, gameImprover.ValueKind);
        Assert.Equal(5m, gameImprover.GetProperty("pointsAwarded").GetDecimal());
    }

    [Fact]
    public async Task Register_WithReferralEmail_IngestsRecommendFriendBountyForReferrer()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var referrerEmail = $"referrer-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, referrerEmail, "Referrer");
        var rootToken = CreateRootAdminToken();

        // Register a new player using referral email.
        var referredEmail = $"referred-{Guid.NewGuid():N}@example.com";
        var registerResult = await GraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email = referredEmail, displayName = "Referred Player", password = "password123", referralEmail = referrerEmail } });

        Assert.False(registerResult.TryGetProperty("errors", out _));

        // Run evaluation so the RECOMMEND_FRIEND event is processed.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        // Check that the referrer received RECOMMEND_FRIEND bounty.
        var (referrerToken, _) = await RegisterAsync(client, $"referrer-check-{Guid.NewGuid():N}@example.com", "Tmp");
        // Re-use the referrer's token from first registration.
        var referrerResult = await GraphQlAsync(
            client,
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) { token }
            }
            """,
            new { input = new { email = referrerEmail, password = "password123" } });

        var referrerLoginToken = referrerResult.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;

        var history = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
                pointsAwarded
              }
            }
            """,
            token: referrerLoginToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        var recommendFriend = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.RecommendFriend);
        Assert.Equal(JsonValueKind.Object, recommendFriend.ValueKind);
        Assert.Equal(5m, recommendFriend.GetProperty("pointsAwarded").GetDecimal());
    }

    [Fact]
    public async Task Register_WithNonExistentReferralEmail_SucceedsWithoutBounty()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var referredEmail = $"referred-noref-{Guid.NewGuid():N}@example.com";
        var registerResult = await GraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email = referredEmail, displayName = "Referred Player", password = "password123", referralEmail = "nonexistent@example.com" } });

        // Registration must succeed even if referral email is invalid.
        Assert.False(registerResult.TryGetProperty("errors", out _));
        var tokenValue = registerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        Assert.NotNull(tokenValue);
    }

    [Fact]
    public async Task ClaimStartupPack_WhenPlayerWasReferred_IngestsRecommendGoodFriendBounty()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var referrerEmail = $"referrer-pack-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, referrerEmail, "Pack Referrer");
        var rootToken = CreateRootAdminToken();

        // Register referred player.
        var referredEmail = $"referred-pack-{Guid.NewGuid():N}@example.com";
        var registerResult = await GraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email = referredEmail, displayName = "Referred Pack Player", password = "password123", referralEmail = referrerEmail } });

        var referredToken = registerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        // Referred player claims startup pack.
        var claimResult = await GraphQlAsync(
            client,
            """
            mutation {
              claimStartupPack {
                isActive
              }
            }
            """,
            token: referredToken);

        Assert.False(claimResult.TryGetProperty("errors", out _));

        // Run evaluation to process RECOMMEND_GOOD_FRIEND event.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        // Check referrer received RECOMMEND_GOOD_FRIEND bounty.
        var referrerLoginResult = await GraphQlAsync(
            client,
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) { token }
            }
            """,
            new { input = new { email = referrerEmail, password = "password123" } });

        var referrerToken = referrerLoginResult.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;

        var history = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
                pointsAwarded
              }
            }
            """,
            token: referrerToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        var recommendGoodFriend = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.RecommendGoodFriend);
        Assert.Equal(JsonValueKind.Object, recommendGoodFriend.ValueKind);
        Assert.Equal(100m, recommendGoodFriend.GetProperty("pointsAwarded").GetDecimal());
    }

    [Fact]
    public async Task SubmitRankingProofEvent_RetweetXPost_IsPendingModerationBeforeApproval()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-retweet-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Retweet User");
        var rootToken = CreateRootAdminToken();

        // Player submits retweet proof.
        var submitResult = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String) {
              submitRankingProofEvent(bountyCode: $bountyCode, proofReference: $proofReference, uniqueScopeKey: $uniqueScopeKey) {
                id
                status
                proofReference
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference = "https://x.com/player/status/12345", uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        var eventItem = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent");
        Assert.Equal("PENDING_MODERATION", eventItem.GetProperty("status").GetString());
        // Proof reference must be hidden from the player on return.
        Assert.Equal(JsonValueKind.Null, eventItem.GetProperty("proofReference").ValueKind);

        // Run evaluation — should NOT award points yet (still PENDING_MODERATION).
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summaryBefore = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        Assert.False(summaryBefore.TryGetProperty("errors", out _));
        Assert.Equal(0m, summaryBefore.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal());
    }

    [Fact]
    public async Task ModerateRankingEvent_WhenApproved_AwardsPointsAfterEvaluation()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-moderate-approve-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Approve User");
        var rootToken = CreateRootAdminToken();

        // Player submits retweet proof.
        var submitResult = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String) {
              submitRankingProofEvent(bountyCode: $bountyCode, proofReference: $proofReference, uniqueScopeKey: $uniqueScopeKey) {
                id
                status
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference = "https://x.com/player/status/approve-test", uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        var eventId = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent").GetProperty("id").GetString()!;

        // Admin approves the event.
        var moderateResult = await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input) {
                id
                status
              }
            }
            """,
            new { input = new { eventId = Guid.Parse(eventId), approve = true, reason = "Valid retweet." } },
            rootToken);

        Assert.False(moderateResult.TryGetProperty("errors", out _));
        Assert.Equal(
            "APPROVED",
            moderateResult.GetProperty("data").GetProperty("moderateRankingEvent").GetProperty("status").GetString());

        // Run evaluation — should now award points.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summary = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        Assert.False(summary.TryGetProperty("errors", out _));
        var totalPoints = summary.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();
        Assert.True(totalPoints > 0m, "Expected points to be awarded after admin approval and evaluation.");
    }

    [Fact]
    public async Task ModerateRankingEvent_WhenRejected_DoesNotAwardPoints()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-moderate-reject-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Reject User");
        var rootToken = CreateRootAdminToken();

        // Player submits retweet proof.
        var submitResult = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String) {
              submitRankingProofEvent(bountyCode: $bountyCode, proofReference: $proofReference, uniqueScopeKey: $uniqueScopeKey) {
                id
                status
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference = "https://x.com/player/status/reject-test", uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        var eventId = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent").GetProperty("id").GetString()!;

        // Admin rejects the event.
        var moderateResult = await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input) {
                id
                status
              }
            }
            """,
            new { input = new { eventId = Guid.Parse(eventId), approve = false, reason = "Fake proof." } },
            rootToken);

        Assert.False(moderateResult.TryGetProperty("errors", out _));
        Assert.Equal(
            "REJECTED",
            moderateResult.GetProperty("data").GetProperty("moderateRankingEvent").GetProperty("status").GetString());

        // Run evaluation — should NOT award any points.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summary = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        Assert.False(summary.TryGetProperty("errors", out _));
        Assert.Equal(0m, summary.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal());
    }

    [Fact]
    public async Task IngestRankingEvent_DiscordPlayer_IsOnceOnly_SecondEventNotAwarded()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-discord-once-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Discord User");
        var rootToken = CreateRootAdminToken();

        // Ingest first DISCORD_PLAYER event and manually approve it.
        var firstIngest = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.DiscordPlayer,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"discord-{Guid.NewGuid():N}",
                    uniqueScopeKey = "discord-handle-unique-1",
                    payloadJson = "{}",
                    proofReference = "PlayerHandle#5678",
                }
            });

        Assert.False(firstIngest.TryGetProperty("errors", out _));
        var firstEventId = firstIngest.GetProperty("data").GetProperty("ingestRankingEvent").GetProperty("id").GetString()!;

        // Admin approves the first event.
        await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input) {
                id
                status
              }
            }
            """,
            new { input = new { eventId = Guid.Parse(firstEventId), approve = true, reason = "Valid Discord." } },
            rootToken);

        // Run evaluation — awards 50 points.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summaryAfterFirst = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        var pointsAfterFirst = summaryAfterFirst.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();
        Assert.True(pointsAfterFirst > 0m, "Expected points after first Discord event.");

        // Ingest a second DISCORD_PLAYER event (different external ID, same player).
        var secondIngest = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id status }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.DiscordPlayer,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow.AddMinutes(5),
                    externalEventId = $"discord-{Guid.NewGuid():N}",
                    uniqueScopeKey = "discord-handle-unique-2",
                    payloadJson = "{}",
                    proofReference = "PlayerHandle#1234",
                }
            });

        Assert.False(secondIngest.TryGetProperty("errors", out _));
        var secondEventId = secondIngest.GetProperty("data").GetProperty("ingestRankingEvent").GetProperty("id").GetString()!;

        // Admin approves the second event too.
        await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input) {
                id
                status
              }
            }
            """,
            new { input = new { eventId = Guid.Parse(secondEventId), approve = true, reason = "Second approval." } },
            rootToken);

        // Run evaluation again — second event should NOT award additional points (Once cooldown).
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summaryAfterSecond = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        var pointsAfterSecond = summaryAfterSecond.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();
        Assert.True(pointsAfterSecond >= pointsAfterFirst);

        var historyAfterSecond = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
              }
            }
            """,
            token: userToken);

        Assert.False(historyAfterSecond.TryGetProperty("errors", out _));
        var historyItems = historyAfterSecond
            .GetProperty("data")
            .GetProperty("myRankingBountyHistory")
            .EnumerateArray()
            .ToList();
        var discordAwards = historyItems
            .Count(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.DiscordPlayer);
        Assert.Equal(1, discordAwards);
    }

    [Fact]
    public async Task IngestRankingEvent_UtcDay_SamePlayerDifferentServers_AwardsOnlyOnceForGameplayBounty()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-cross-server-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Cross Server User");
        var rootToken = CreateRootAdminToken();
        await RegisterServerAsync(client, "server-eu");
        await RegisterServerAsync(client, "server-us");

        // Ingest MANUFACTURER from two different servers on same day.
        await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "server-eu",
                    eventType = MasterRankingBountyCodes.Manufacturer,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"mfr-eu-{Guid.NewGuid():N}",
                    uniqueScopeKey = "mfr-eu-day",
                    payloadJson = "{}",
                }
            });

        await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "server-us",
                    eventType = MasterRankingBountyCodes.Manufacturer,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"mfr-us-{Guid.NewGuid():N}",
                    uniqueScopeKey = "mfr-us-day",
                    payloadJson = "{}",
                }
            });

        // Run evaluation — gameplay bounties are UTC-day scoped across all servers, so only one award is allowed.
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var history = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
                serverKey
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        var manufacturerItems = items.Where(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.Manufacturer).ToList();
        Assert.Single(manufacturerItems);
      }

      [Fact]
      public async Task IngestRankingEvent_LoginToGame_SamePlayerDifferentServers_AwardsPerServer()
      {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-login-cross-server-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Cross Server Login User");
        var rootToken = CreateRootAdminToken();
        await RegisterServerAsync(client, "server-eu");
        await RegisterServerAsync(client, "server-us");

        await GraphQlAsync(
          client,
          """
          mutation Ingest($input: IngestRankingEventInput!) {
            ingestRankingEvent(input: $input) { id }
          }
          """,
          new
          {
            input = new
            {
              registrationKey = "test-registration-key",
              serverKey = "server-eu",
              eventType = MasterRankingBountyCodes.LoginToGame,
              playerEmail = userEmail,
              occurredAtUtc = DateTime.UtcNow,
              externalEventId = $"login-eu-{Guid.NewGuid():N}",
              uniqueScopeKey = "login-eu-day",
              payloadJson = "{}",
            }
          });

        await GraphQlAsync(
          client,
          """
          mutation Ingest($input: IngestRankingEventInput!) {
            ingestRankingEvent(input: $input) { id }
          }
          """,
          new
          {
            input = new
            {
              registrationKey = "test-registration-key",
              serverKey = "server-us",
              eventType = MasterRankingBountyCodes.LoginToGame,
              playerEmail = userEmail,
              occurredAtUtc = DateTime.UtcNow,
              externalEventId = $"login-us-{Guid.NewGuid():N}",
              uniqueScopeKey = "login-us-day",
              payloadJson = "{}",
            }
          });

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var history = await GraphQlAsync(
          client,
          """
          query {
            myRankingBountyHistory {
            bountyCode
            serverKey
            }
          }
          """,
          token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var items = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        var loginItems = items.Where(item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.LoginToGame).ToList();
        Assert.Equal(2, loginItems.Count);
        var serverKeys = loginItems.Select(item => item.GetProperty("serverKey").GetString()).ToHashSet();
        Assert.Contains("server-eu", serverKeys);
        Assert.Contains("server-us", serverKeys);
    }

    [Fact]
    public async Task RankingAdminDashboard_ProofReference_IsVisibleToAdminButHiddenFromPlayer()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-privacy-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Privacy User");
        var rootToken = CreateRootAdminToken();

        var proofUrl = "https://x.com/player/status/privacy-test-999";

        // Player submits retweet proof.
        var submitResult = await GraphQlAsync(
            client,
            """
            mutation($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String) {
              submitRankingProofEvent(bountyCode: $bountyCode, proofReference: $proofReference, uniqueScopeKey: $uniqueScopeKey) {
                id
                status
                proofReference
              }
            }
            """,
            new { bountyCode = MasterRankingBountyCodes.RetweetXPost, proofReference = proofUrl, uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        // Proof reference is hidden from player on mutation return.
        var returnedProof = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent").GetProperty("proofReference");
        Assert.Equal(JsonValueKind.Null, returnedProof.ValueKind);

        // Admin can see the proof reference in the moderation queue.
        var dashboard = await GraphQlAsync(
            client,
            """
            query {
              rankingAdminDashboard {
                pendingModerationEvents {
                  playerEmail
                  proofReference
                  eventType
                }
              }
            }
            """,
            token: rootToken);

        Assert.False(dashboard.TryGetProperty("errors", out _));
        var pendingEvents = dashboard.GetProperty("data").GetProperty("rankingAdminDashboard").GetProperty("pendingModerationEvents").EnumerateArray().ToList();
        var retweetEvent = pendingEvents.FirstOrDefault(e => e.GetProperty("playerEmail").GetString() == userEmail);
        Assert.Equal(JsonValueKind.Object, retweetEvent.ValueKind);
        Assert.Equal(proofUrl, retweetEvent.GetProperty("proofReference").GetString());
    }

    [Fact]
    public async Task IngestRankingEvent_UnknownEmail_AutoCreatesMasterAccountAndPreservesRewardsAfterRegister()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-shadow-{Guid.NewGuid():N}@example.com";
        var rootToken = CreateRootAdminToken();

        var ingestResult = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) {
                id
                status
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.FxTrader,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"fx-shadow-{Guid.NewGuid():N}",
                    uniqueScopeKey = "swap-shadow-1",
                    payloadJson = "{}",
                }
            });

        Assert.False(ingestResult.TryGetProperty("errors", out _));

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var registerResult = await GraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email = userEmail, displayName = "Shadow Converted", password = "password123" } });

        Assert.False(registerResult.TryGetProperty("errors", out _));
        var userToken = registerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userToken));

        var history = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var historyItems = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        Assert.Contains(historyItems, item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.FxTrader);
    }

    [Fact]
    public async Task IngestRankingEvent_UnknownEmail_ManufacturerAndWholesaler_AutoCreatesAccountAndAwardsBoth()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-active-{Guid.NewGuid():N}@example.com";
        var rootToken = CreateRootAdminToken();

        var manufacturerResult = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) {
                id
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.Manufacturer,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"manufacturer-{Guid.NewGuid():N}",
                    uniqueScopeKey = $"manufacturer:{Guid.NewGuid():N}",
                    payloadJson = "{}",
                }
            });

        Assert.False(manufacturerResult.TryGetProperty("errors", out _));

        var wholesalerResult = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) {
                id
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.Wholesaler,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"wholesaler-{Guid.NewGuid():N}",
                    uniqueScopeKey = $"wholesaler:{Guid.NewGuid():N}",
                    payloadJson = "{}",
                }
            });

        Assert.False(wholesalerResult.TryGetProperty("errors", out _));

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var registerResult = await GraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email = userEmail, displayName = "Active User", password = "password123" } });

        Assert.False(registerResult.TryGetProperty("errors", out _));
        var userToken = registerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userToken));

        var history = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyHistory {
                bountyCode
              }
            }
            """,
            token: userToken);

        Assert.False(history.TryGetProperty("errors", out _));
        var historyItems = history.GetProperty("data").GetProperty("myRankingBountyHistory").EnumerateArray().ToList();
        Assert.Contains(historyItems, item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.Manufacturer);
        Assert.Contains(historyItems, item => item.GetProperty("bountyCode").GetString() == MasterRankingBountyCodes.Wholesaler);
    }

    [Fact]
    public async Task MyRankingBountyDashboard_WhenAwardedToday_ShowsNextAvailabilityUtc()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-dashboard-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Dashboard User");
        var rootToken = CreateRootAdminToken();

        var ingestResult = await GraphQlAsync(
            client,
            """
            mutation Ingest($input: IngestRankingEventInput!) {
              ingestRankingEvent(input: $input) {
                id
              }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey = "capitalism-eu-1",
                    eventType = MasterRankingBountyCodes.GameImprover,
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"improver-{Guid.NewGuid():N}",
                    uniqueScopeKey = $"support-{Guid.NewGuid():N}",
                    payloadJson = "{}",
                }
            });

        Assert.False(ingestResult.TryGetProperty("errors", out _));

        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var dashboard = await GraphQlAsync(
            client,
            """
            query {
              myRankingBountyDashboard {
                code
                awardedToday
                isAvailableNow
                nextAvailableAtUtc
                totalAwards
              }
            }
            """,
            token: userToken);

        Assert.False(dashboard.TryGetProperty("errors", out _));

        var items = dashboard.GetProperty("data").GetProperty("myRankingBountyDashboard").EnumerateArray().ToList();
        var gameImprover = items.FirstOrDefault(item => item.GetProperty("code").GetString() == MasterRankingBountyCodes.GameImprover);

        Assert.Equal(JsonValueKind.Object, gameImprover.ValueKind);
        Assert.True(gameImprover.GetProperty("awardedToday").GetBoolean());
        Assert.False(gameImprover.GetProperty("isAvailableNow").GetBoolean());
        Assert.Equal(JsonValueKind.String, gameImprover.GetProperty("nextAvailableAtUtc").ValueKind);
        Assert.True(gameImprover.GetProperty("totalAwards").GetInt32() >= 1);
    }

    private async Task<(string Token, string PlayerId)> RegisterAsync(HttpClient client, string email, string displayName)
    {
        var result = await GraphQlAsync(
        client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email, displayName, password = "password123" } });

        var payload = result.GetProperty("data").GetProperty("register");
        return (
            payload.GetProperty("token").GetString()!,
            payload.GetProperty("player").GetProperty("id").GetString()!);
    }

    private async Task RegisterServerAsync(HttpClient client, string serverKey)
    {
        await GraphQlAsync(
            client,
            """
            mutation RegisterServer($input: RegisterGameServerInput!) {
              registerGameServer(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    registrationKey = "test-registration-key",
                    serverKey,
                    displayName = $"Server {serverKey}",
                    description = "Test shard registration",
                    region = "EU",
                    environment = "test",
                    backendUrl = $"https://{serverKey}.example.com",
                    graphqlUrl = $"https://{serverKey}.example.com/graphql",
                    frontendUrl = $"https://{serverKey}.example.com/app",
                    version = "1.0.0",
                    playerCount = 1,
                    companyCount = 1,
                    currentTick = 1,
                }
            });
    }

    private async Task<JsonElement> GraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string CreateRootAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SharedJwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: SharedJwtIssuer,
            audience: SharedJwtAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "root@example.com"),
                new Claim(ClaimTypes.Name, "Root Admin"),
                new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
