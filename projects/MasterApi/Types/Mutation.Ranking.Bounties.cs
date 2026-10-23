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
    public async Task<RankingBountyDefinitionInfo> UpsertRankingBountyDefinition(
        UpsertRankingBountyDefinitionInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking bounty configuration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var code = input.Code.Trim().ToUpperInvariant();
        if (!MasterRankingBountyCodes.All.Contains(code))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Unknown bounty code.")
                    .SetCode("UNKNOWN_BOUNTY_CODE")
                    .Build());
        }

        if (!RankingCooldownMode.All.Contains(input.CooldownMode))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid cooldown mode.")
                    .SetCode("INVALID_COOLDOWN_MODE")
                    .Build());
        }

        if (!RankingProofRequirement.All.Contains(input.ProofRequirement))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid proof requirement.")
                    .SetCode("INVALID_PROOF_REQUIREMENT")
                    .Build());
        }

        if (!RankingVisibilityScope.All.Contains(input.VisibilityScope))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid visibility scope.")
                    .SetCode("INVALID_VISIBILITY_SCOPE")
                    .Build());
        }

        var now = DateTime.UtcNow;
        var existing = input.Id.HasValue
            ? await db.MasterRankingBountyDefinitions.FirstOrDefaultAsync(definition => definition.Id == input.Id.Value)
            : await db.MasterRankingBountyDefinitions.FirstOrDefaultAsync(definition => definition.Code == code);

        var previousValueJson = existing is null
            ? "{}"
            : JsonSerializer.Serialize(existing);

        if (existing is null)
        {
            existing = new MasterRankingBountyDefinition
            {
                Id = Guid.NewGuid(),
                Code = code,
                CreatedAtUtc = now,
            };
            db.MasterRankingBountyDefinitions.Add(existing);
        }

        existing.DisplayName = input.DisplayName.Trim();
        existing.Description = input.Description.Trim();
        existing.RewardPoints = input.RewardPoints;
        existing.IsEnabled = input.IsEnabled;
        existing.IsVisibleToPlayers = input.IsVisibleToPlayers;
        existing.RequiresModeration = input.RequiresModeration;
        existing.CooldownMode = input.CooldownMode;
        existing.SourceEventType = input.SourceEventType.Trim().ToUpperInvariant();
        existing.ProofRequirement = input.ProofRequirement;
        existing.VisibilityScope = input.VisibilityScope;
        existing.ValidationSettingsJson = string.IsNullOrWhiteSpace(input.ValidationSettingsJson) ? "{}" : input.ValidationSettingsJson;
        existing.UpdatedAtUtc = now;

        db.MasterRankingBountyAudits.Add(new MasterRankingBountyAudit
        {
            Id = Guid.NewGuid(),
            BountyDefinitionId = existing.Id,
            ChangedByEmail = callerEmail,
            ChangeType = input.Id.HasValue ? "UPDATED" : "CREATED",
            PreviousValueJson = previousValueJson,
            NewValueJson = JsonSerializer.Serialize(existing),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();

        return new RankingBountyDefinitionInfo
        {
            Id = existing.Id,
            Code = existing.Code,
            DisplayName = existing.DisplayName,
            Description = existing.Description,
            RewardPoints = existing.RewardPoints,
            IsEnabled = existing.IsEnabled,
            IsVisibleToPlayers = existing.IsVisibleToPlayers,
            RequiresModeration = existing.RequiresModeration,
            CooldownMode = existing.CooldownMode,
            SourceEventType = existing.SourceEventType,
            ProofRequirement = existing.ProofRequirement,
            VisibilityScope = existing.VisibilityScope,
            ValidationSettingsJson = existing.ValidationSettingsJson,
            UpdatedAtUtc = existing.UpdatedAtUtc,
        };
    }
}
