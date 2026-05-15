using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>Returns all currently active global economic shock events (public — no auth required).</summary>
    public async Task<List<GlobalEvent>> ActiveGlobalEvents([Service] AppDbContext db)
    {
        var tick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultAsync();

        return await db.GlobalEvents
            .AsNoTracking()
            .Where(ge => ge.IsActive
                      && ge.StartTick <= tick
                      && (ge.StartTick + ge.DurationTicks) > tick)
            .Include(ge => ge.AffectedCity)
            .OrderByDescending(ge => ge.StartTick)
            .ToListAsync();
    }

    /// <summary>Returns recent global events (both active and resolved), newest first.</summary>
    public async Task<List<GlobalEvent>> GlobalEventHistory(
        [Service] AppDbContext db,
        int limit = 20)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return await db.GlobalEvents
            .AsNoTracking()
            .Include(ge => ge.AffectedCity)
            .OrderByDescending(ge => ge.CreatedAtUtc)
            .Take(safeLimit)
            .ToListAsync();
    }
}
