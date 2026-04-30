using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MasterApi.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Tests;

public sealed class RankingIntegrationTests
{
    private const string SharedJwtIssuer = "Capitalism";
    private const string SharedJwtAudience = "Capitalism";
    private const string SharedJwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";

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
                    eventType = "FX_TRADER",
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
        Assert.Single(historyItems, item => item.GetProperty("bountyCode").GetString() == "FX_TRADER");
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
                    eventType = "FX_TRADER",
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
        var gameImprover = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == "GAME_IMPROVER");
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
        var recommendFriend = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == "RECOMMEND_FRIEND");
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
        var recommendGoodFriend = items.FirstOrDefault(item => item.GetProperty("bountyCode").GetString() == "RECOMMEND_GOOD_FRIEND");
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
            new { bountyCode = "RETWEET_X_POST", proofReference = "https://x.com/player/status/12345", uniqueScopeKey = (string?)null },
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
            new { bountyCode = "RETWEET_X_POST", proofReference = "https://x.com/player/status/approve-test", uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        var eventId = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent").GetProperty("id").GetString()!;

        // Admin approves the event.
        var moderateResult = await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input)
            }
            """,
            new { input = new { eventId = Guid.Parse(eventId), approve = true, reason = "Valid retweet." } },
            rootToken);

        Assert.False(moderateResult.TryGetProperty("errors", out _));
        Assert.True(moderateResult.GetProperty("data").GetProperty("moderateRankingEvent").GetBoolean());

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
            new { bountyCode = "RETWEET_X_POST", proofReference = "https://x.com/player/status/reject-test", uniqueScopeKey = (string?)null },
            userToken);

        Assert.False(submitResult.TryGetProperty("errors", out _));
        var eventId = submitResult.GetProperty("data").GetProperty("submitRankingProofEvent").GetProperty("id").GetString()!;

        // Admin rejects the event.
        var moderateResult = await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input)
            }
            """,
            new { input = new { eventId = Guid.Parse(eventId), approve = false, reason = "Fake proof." } },
            rootToken);

        Assert.False(moderateResult.TryGetProperty("errors", out _));

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
                    eventType = "DISCORD_PLAYER",
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"discord-{Guid.NewGuid():N}",
                    uniqueScopeKey = "discord-handle-unique-1",
                    payloadJson = "{}",
                    proofReference = "PlayerHandle#1234",
                }
            });

        Assert.False(firstIngest.TryGetProperty("errors", out _));
        var firstEventId = firstIngest.GetProperty("data").GetProperty("ingestRankingEvent").GetProperty("id").GetString()!;

        // Admin approves the first event.
        await GraphQlAsync(
            client,
            """
            mutation($input: ModerateRankingEventInput!) {
              moderateRankingEvent(input: $input)
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
                    eventType = "DISCORD_PLAYER",
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
              moderateRankingEvent(input: $input)
            }
            """,
            new { input = new { eventId = Guid.Parse(secondEventId), approve = true, reason = "Second approval." } },
            rootToken);

        // Run evaluation again — second event should NOT award additional points (Once cooldown).
        await GraphQlAsync(client, "mutation { runRankingEvaluationNow { id } }", token: rootToken);

        var summaryAfterSecond = await GraphQlAsync(client, "query { myRankingSummary { totalPoints } }", token: userToken);
        var pointsAfterSecond = summaryAfterSecond.GetProperty("data").GetProperty("myRankingSummary").GetProperty("totalPoints").GetDecimal();
        Assert.Equal(pointsAfterFirst, pointsAfterSecond);
    }

    [Fact]
    public async Task IngestRankingEvent_UtcDayPerServer_SamePlayerDifferentServers_BothAwarded()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userEmail = $"rank-cross-server-{Guid.NewGuid():N}@example.com";
        var (userToken, _) = await RegisterAsync(client, userEmail, "Cross Server User");
        var rootToken = CreateRootAdminToken();

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
                    eventType = "MANUFACTURER",
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
                    eventType = "MANUFACTURER",
                    playerEmail = userEmail,
                    occurredAtUtc = DateTime.UtcNow,
                    externalEventId = $"mfr-us-{Guid.NewGuid():N}",
                    uniqueScopeKey = "mfr-us-day",
                    payloadJson = "{}",
                }
            });

        // Run evaluation — MANUFACTURER is UtcDayPerServer, so player gets two awards (one per server).
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
        var manufacturerItems = items.Where(item => item.GetProperty("bountyCode").GetString() == "MANUFACTURER").ToList();
        Assert.Equal(2, manufacturerItems.Count);
        var serverKeys = manufacturerItems.Select(item => item.GetProperty("serverKey").GetString()).ToHashSet();
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
            new { bountyCode = "RETWEET_X_POST", proofReference = proofUrl, uniqueScopeKey = (string?)null },
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
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
