using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<NpcCompanySummaryResult> CreateNpcCompany(
        CreateNpcCompanyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            httpContextAccessor.HttpContext.RequestAborted);

        if (!NpcArchetype.All.Contains(input.Archetype, StringComparer.Ordinal))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid NPC archetype.")
                    .SetCode("INVALID_NPC_ARCHETYPE")
                    .Build());
        }

        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == input.HomeCityId, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
                    .Build());

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"npc-{suffix}@npc.local",
            DisplayName = input.Name,
            Gender = PlayerGender.Unspecified,
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Company,
            CreatedAtUtc = DateTime.UtcNow,
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        player.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Player>()
            .HashPassword(player, $"NpcCreate!{suffix}");
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = input.Name,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = await GetCurrentTickAsync(db),
        };
        db.Companies.Add(company);
        player.ActiveCompanyId = company.Id;

        var account = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
            db,
            company.Id,
            city.CurrencyCode,
            cancellationToken: httpContextAccessor.HttpContext.RequestAborted);
        account.Balance += Math.Max(100_000m, input.StartingCash);

        var npc = new NpcCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            HomeCityId = city.Id,
            Name = input.Name,
            Archetype = input.Archetype,
            DifficultyLevel = Math.Clamp(input.DifficultyLevel, 1, 5),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.NpcCompanies.Add(npc);

        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);

        return new NpcCompanySummaryResult
        {
            Id = npc.Id,
            CompanyId = company.Id,
            Name = npc.Name,
            Archetype = npc.Archetype,
            DifficultyLevel = npc.DifficultyLevel,
            HomeCityId = npc.HomeCityId,
            HomeCityName = city.Name,
            IsActive = npc.IsActive,
            CreatedAtUtc = npc.CreatedAtUtc,
            BuildingCount = 0,
        };
    }

    [Authorize]
    public async Task<NpcCompanySummaryResult> PauseNpcCompany(
        ManageNpcCompanyActivityInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            httpContextAccessor.HttpContext.RequestAborted);
        return await SetNpcActiveStateAsync(db, input.NpcCompanyId, isActive: false, httpContextAccessor.HttpContext.RequestAborted);
    }

    [Authorize]
    public async Task<NpcCompanySummaryResult> ResumeNpcCompany(
        ManageNpcCompanyActivityInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            httpContextAccessor.HttpContext.RequestAborted);
        return await SetNpcActiveStateAsync(db, input.NpcCompanyId, isActive: true, httpContextAccessor.HttpContext.RequestAborted);
    }

    private static async Task<NpcCompanySummaryResult> SetNpcActiveStateAsync(
        AppDbContext db,
        Guid npcCompanyId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var npc = await db.NpcCompanies
            .Include(item => item.HomeCity)
            .FirstOrDefaultAsync(item => item.Id == npcCompanyId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("NPC company not found.")
                    .SetCode("NPC_COMPANY_NOT_FOUND")
                    .Build());

        npc.IsActive = isActive;
        db.NpcDecisionLogs.Add(new NpcDecisionLog
        {
            Id = Guid.NewGuid(),
            NpcCompanyId = npc.Id,
            Tick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync(cancellationToken),
            ActionType = isActive ? "RESUME" : "PAUSE",
            Outcome = isActive ? "NPC activity resumed by admin." : "NPC activity paused by admin.",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
        var buildingCount = await db.Buildings.CountAsync(building => building.CompanyId == npc.CompanyId && building.DestroyedAtUtc == null, cancellationToken);

        return new NpcCompanySummaryResult
        {
            Id = npc.Id,
            CompanyId = npc.CompanyId,
            Name = npc.Name,
            Archetype = npc.Archetype,
            DifficultyLevel = npc.DifficultyLevel,
            HomeCityId = npc.HomeCityId,
            HomeCityName = npc.HomeCity.Name,
            IsActive = npc.IsActive,
            CreatedAtUtc = npc.CreatedAtUtc,
            BuildingCount = buildingCount,
        };
    }
}

public sealed class CreateNpcCompanyInput
{
    public string Name { get; set; } = string.Empty;
    public string Archetype { get; set; } = NpcArchetype.Conglomerate;
    public int DifficultyLevel { get; set; } = 2;
    public Guid HomeCityId { get; set; }
    public decimal StartingCash { get; set; } = 300_000m;
}

public sealed class ManageNpcCompanyActivityInput
{
    public Guid NpcCompanyId { get; set; }
}
