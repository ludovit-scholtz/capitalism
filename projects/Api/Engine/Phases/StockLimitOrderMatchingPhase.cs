using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Matches stock exchange limit orders each tick using price-time priority.
/// </summary>
public sealed class StockLimitOrderMatchingPhase : ITickPhase
{
    public string Name => "StockLimitOrderMatching";
    public int Order => 910;

    public async Task ProcessAsync(TickContext context)
    {
        var companyIds = await context.Db.LimitOrders
            .Where(order => (order.Status == Data.Entities.LimitOrderStatus.Open || order.Status == Data.Entities.LimitOrderStatus.PartiallyFilled))
            .Select(order => order.CompanyId)
            .Distinct()
            .ToListAsync();

        foreach (var companyId in companyIds)
        {
            await StockLimitOrderMatchingService.MatchForCompanyAsync(context.Db, companyId, context.CurrentTick);
        }
    }
}
