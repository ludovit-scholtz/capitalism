using System.Security.Claims;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingRunInfo> RunRankingEvaluationNow(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] MasterRankingService rankingService)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking evaluator execution requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var run = await rankingService.EvaluateHourlyAsync();
        return ToRankingRunInfo(run);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingRunInfo> RunRankingDailyDecayNow(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] MasterRankingService rankingService)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking decay execution requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var run = await rankingService.ApplyDailyDecayAsync();
        return ToRankingRunInfo(run);
    }

    private static RankingRunInfo ToRankingRunInfo(MasterRankingEvaluationRun run)
    {
        return new RankingRunInfo
        {
            Id = run.Id,
            RunType = run.RunType,
            Status = run.Status,
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = run.FinishedAtUtc,
            ProcessedEvents = run.ProcessedEvents,
            RewardRecordsCreated = run.RewardRecordsCreated,
            TotalPointsAwarded = run.TotalPointsAwarded,
            TotalPointsBeforeDecay = run.TotalPointsBeforeDecay,
            TotalPointsAfterDecay = run.TotalPointsAfterDecay,
            Notes = run.Notes,
        };
    }
}
