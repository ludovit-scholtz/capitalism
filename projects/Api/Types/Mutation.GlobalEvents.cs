using System.Security.Claims;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Admin-only: manually trigger a global economic shock event.
    /// Useful for live-ops interventions and storyline events.
    /// </summary>
    [Authorize]
    public async Task<GlobalEvent> TriggerGlobalEvent(
        TriggerGlobalEventInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db, httpContextAccessor.HttpContext.User, ct);

        if (!GlobalEventType.All.Contains(input.EventType))
            throw new GraphQLException(new Error("INVALID_EVENT_TYPE", $"Unknown event type: {input.EventType}"));

        if (!new[] { GlobalEventSeverity.Minor, GlobalEventSeverity.Moderate, GlobalEventSeverity.Major, GlobalEventSeverity.Catastrophic }
                .Contains(input.Severity))
            throw new GraphQLException(new Error("INVALID_SEVERITY", $"Unknown severity: {input.Severity}"));

        var gameState = await db.GameStates.FirstAsync(ct);

        City? affectedCity = null;
        if (input.AffectedCityId.HasValue)
        {
            affectedCity = await db.Cities.FindAsync([input.AffectedCityId.Value], ct)
                ?? throw new GraphQLException(new Error("CITY_NOT_FOUND", "Affected city not found."));
        }

        var adminId = httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        var evt = new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = input.EventType,
            Severity = input.Severity,
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            IsActive = true,
            StartTick = gameState.CurrentTick,
            DurationTicks = input.DurationTicks,
            AffectedCityId = affectedCity?.Id,
            AffectedCity = affectedCity,
            OperatingCostMultiplier = input.OperatingCostMultiplier,
            TradeRouteMultiplier = input.TradeRouteMultiplier,
            RdMultiplier = input.RdMultiplier,
            MineEfficiencyMultiplier = input.MineEfficiencyMultiplier,
            CreatedAtUtc = DateTime.UtcNow,
            TriggeredByAdminId = adminId,
        };

        db.GlobalEvents.Add(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }

    /// <summary>Admin-only: manually resolve (deactivate) an active global event ahead of schedule.</summary>
    [Authorize]
    public async Task<GlobalEvent> ResolveGlobalEvent(
        Guid id,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db, httpContextAccessor.HttpContext.User, ct);

        var evt = await db.GlobalEvents.FindAsync([id], ct)
            ?? throw new GraphQLException(new Error("EVENT_NOT_FOUND", "Global event not found."));

        if (!evt.IsActive)
            throw new GraphQLException(new Error("EVENT_ALREADY_RESOLVED", "This event is already resolved."));

        evt.IsActive = false;
        evt.ResolvedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return evt;
    }
}

public sealed record TriggerGlobalEventInput(
    string EventType,
    string Severity,
    string Title,
    string Description,
    long DurationTicks,
    Guid? AffectedCityId,
    decimal OperatingCostMultiplier,
    decimal TradeRouteMultiplier,
    decimal RdMultiplier,
    decimal MineEfficiencyMultiplier);
