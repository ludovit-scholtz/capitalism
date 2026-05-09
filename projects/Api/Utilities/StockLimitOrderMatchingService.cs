using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class StockLimitOrderMatchingService
{
    public static async Task MatchForCompanyAsync(AppDbContext db, Guid companyId, long currentTick, CancellationToken ct = default)
    {
        var buyOrders = await db.LimitOrders
            .Where(order => order.CompanyId == companyId
                && order.Side == LimitOrderSide.Buy
                && (order.Status == LimitOrderStatus.Open || order.Status == LimitOrderStatus.PartiallyFilled))
            .OrderByDescending(order => order.LimitPrice)
            .ThenBy(order => order.CreatedAtTick)
            .ThenBy(order => order.CreatedAtUtc)
            .ToListAsync(ct);

        var sellOrders = await db.LimitOrders
            .Where(order => order.CompanyId == companyId
                && order.Side == LimitOrderSide.Sell
                && (order.Status == LimitOrderStatus.Open || order.Status == LimitOrderStatus.PartiallyFilled))
            .OrderBy(order => order.LimitPrice)
            .ThenBy(order => order.CreatedAtTick)
            .ThenBy(order => order.CreatedAtUtc)
            .ToListAsync(ct);

        if (buyOrders.Count == 0 || sellOrders.Count == 0)
        {
            return;
        }

        var company = await db.Companies.FirstOrDefaultAsync(candidate => candidate.Id == companyId, ct);
        if (company is null)
        {
            return;
        }

        var shareholdings = await db.Shareholdings
            .Where(holding => holding.CompanyId == companyId)
            .ToListAsync(ct);
        var bankAccounts = await db.BankAccounts
            .Where(account => account.ClosedAtUtc == null)
            .ToDictionaryAsync(account => account.Id, ct);

        var buyIndex = 0;
        var sellIndex = 0;
        while (buyIndex < buyOrders.Count && sellIndex < sellOrders.Count)
        {
            var buy = buyOrders[buyIndex];
            var sell = sellOrders[sellIndex];

            if (buy.LimitPrice < sell.LimitPrice)
            {
                break;
            }

            var buyRemaining = buy.Quantity - buy.FilledQuantity;
            var sellRemaining = sell.Quantity - sell.FilledQuantity;
            if (buyRemaining <= 0)
            {
                buyIndex++;
                continue;
            }

            if (sellRemaining <= 0)
            {
                sellIndex++;
                continue;
            }

            if (!bankAccounts.TryGetValue(buy.SettlementBankAccountId, out var buyerAccount))
            {
                CancelOrderWithoutRelease(buy, currentTick);
                buyIndex++;
                continue;
            }

            if (!bankAccounts.TryGetValue(sell.SettlementBankAccountId, out var sellerAccount))
            {
                CancelOrderWithoutRelease(sell, currentTick);
                sellIndex++;
                continue;
            }

            var tradeQuantity = Math.Min(buyRemaining, sellRemaining);
            var sellHolding = FindHolding(shareholdings, companyId, sell.OwnerPlayerId, sell.OwnerCompanyId);
            if (sellHolding is null || sellHolding.ShareCount <= 0m)
            {
                CancelOrderWithoutRelease(sell, currentTick);
                sellIndex++;
                continue;
            }

            var availableSellShares = decimal.ToInt32(decimal.Truncate(sellHolding.ShareCount));
            if (availableSellShares <= 0)
            {
                CancelOrderWithoutRelease(sell, currentTick);
                sellIndex++;
                continue;
            }

            if (tradeQuantity > availableSellShares)
            {
                tradeQuantity = availableSellShares;
            }

            if (tradeQuantity <= 0)
            {
                sellIndex++;
                continue;
            }

            var tradePrice = sell.LimitPrice;
            var limitReservedForFill = decimal.Round(buy.LimitPrice * tradeQuantity, 4, MidpointRounding.AwayFromZero);
            var tradeCost = decimal.Round(tradePrice * tradeQuantity, 4, MidpointRounding.AwayFromZero);
            if (buy.ReservedCashRemaining < limitReservedForFill)
            {
                CancelBuyAndRelease(buy, buyerAccount, currentTick);
                buyIndex++;
                continue;
            }

            buy.ReservedCashRemaining -= limitReservedForFill;
            var rebate = decimal.Round(limitReservedForFill - tradeCost, 4, MidpointRounding.AwayFromZero);
            if (rebate > 0m)
            {
                buyerAccount.Balance += rebate;
            }

            sellerAccount.Balance += tradeCost;
            sellHolding.ShareCount = decimal.Round(sellHolding.ShareCount - tradeQuantity, 4, MidpointRounding.AwayFromZero);
            if (sellHolding.ShareCount <= 0m)
            {
                db.Shareholdings.Remove(sellHolding);
                shareholdings.Remove(sellHolding);
            }

            var buyHolding = FindHolding(shareholdings, companyId, buy.OwnerPlayerId, buy.OwnerCompanyId);
            if (buyHolding is null)
            {
                buyHolding = new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    OwnerPlayerId = buy.OwnerPlayerId,
                    OwnerCompanyId = buy.OwnerCompanyId,
                    ShareCount = 0m,
                };
                db.Shareholdings.Add(buyHolding);
                shareholdings.Add(buyHolding);
            }

            buyHolding.ShareCount = decimal.Round(buyHolding.ShareCount + tradeQuantity, 4, MidpointRounding.AwayFromZero);
            buy.FilledQuantity += tradeQuantity;
            sell.FilledQuantity += tradeQuantity;
            buy.UpdatedAtTick = currentTick;
            buy.UpdatedAtUtc = DateTime.UtcNow;
            sell.UpdatedAtTick = currentTick;
            sell.UpdatedAtUtc = DateTime.UtcNow;
            buy.Status = buy.FilledQuantity >= buy.Quantity ? LimitOrderStatus.Filled : LimitOrderStatus.PartiallyFilled;
            sell.Status = sell.FilledQuantity >= sell.Quantity ? LimitOrderStatus.Filled : LimitOrderStatus.PartiallyFilled;

            if (buy.Status == LimitOrderStatus.Filled && buy.ReservedCashRemaining > 0m)
            {
                buyerAccount.Balance += buy.ReservedCashRemaining;
                buy.ReservedCashRemaining = 0m;
            }

            db.LimitOrderExecutions.Add(new LimitOrderExecution
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                StockSymbol = buy.StockSymbol,
                BuyOrderId = buy.Id,
                SellOrderId = sell.Id,
                Price = tradePrice,
                Quantity = tradeQuantity,
                ExecutedAtTick = currentTick,
                ExecutedAtUtc = DateTime.UtcNow,
            });

            await RecordSharePriceHistoryAsync(db, companyId, tradePrice, currentTick, ct);
            RecordPersonalTradeIfNeeded(db, buy, company, tradeQuantity, tradePrice, currentTick, TradeDirection.Buy);
            RecordPersonalTradeIfNeeded(db, sell, company, tradeQuantity, tradePrice, currentTick, TradeDirection.Sell);
            RecordCompanyLedgerIfNeeded(db, buy, company, tradeQuantity, tradePrice, currentTick, isBuy: true);
            RecordCompanyLedgerIfNeeded(db, sell, company, tradeQuantity, tradePrice, currentTick, isBuy: false);

            if (buy.Status == LimitOrderStatus.Filled)
            {
                buyIndex++;
            }

            if (sell.Status == LimitOrderStatus.Filled)
            {
                sellIndex++;
            }
        }
    }

    public static decimal ComputeReservedBuyCash(decimal limitPrice, int quantity)
        => decimal.Round(limitPrice * quantity, 4, MidpointRounding.AwayFromZero);

    public static async Task<decimal> GetReservedBuyCashAsync(
        AppDbContext db,
        Guid? ownerPlayerId,
        Guid? ownerCompanyId,
        Guid settlementBankAccountId,
        CancellationToken ct = default)
    {
        return await db.LimitOrders
            .Where(order =>
                order.Side == LimitOrderSide.Buy
                && (order.Status == LimitOrderStatus.Open || order.Status == LimitOrderStatus.PartiallyFilled)
                && order.SettlementBankAccountId == settlementBankAccountId
                && order.OwnerPlayerId == ownerPlayerId
                && order.OwnerCompanyId == ownerCompanyId)
            .SumAsync(order => order.ReservedCashRemaining, ct);
    }

    public static async Task<int> GetReservedSellSharesAsync(
        AppDbContext db,
        Guid companyId,
        Guid? ownerPlayerId,
        Guid? ownerCompanyId,
        CancellationToken ct = default)
    {
        return await db.LimitOrders
            .Where(order =>
                order.CompanyId == companyId
                && order.Side == LimitOrderSide.Sell
                && (order.Status == LimitOrderStatus.Open || order.Status == LimitOrderStatus.PartiallyFilled)
                && order.OwnerPlayerId == ownerPlayerId
                && order.OwnerCompanyId == ownerCompanyId)
            .SumAsync(order => order.Quantity - order.FilledQuantity, ct);
    }

    public static void CancelOrderAndRelease(LimitOrder order, BankAccount settlementAccount, long currentTick)
    {
        if (order.Side == LimitOrderSide.Buy && order.ReservedCashRemaining > 0m)
        {
            settlementAccount.Balance += order.ReservedCashRemaining;
            order.ReservedCashRemaining = 0m;
        }

        order.Status = LimitOrderStatus.Cancelled;
        order.UpdatedAtTick = currentTick;
        order.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void CancelOrderWithoutRelease(LimitOrder order, long currentTick)
    {
        order.Status = LimitOrderStatus.Cancelled;
        order.UpdatedAtTick = currentTick;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.ReservedCashRemaining = 0m;
    }

    private static void CancelBuyAndRelease(LimitOrder order, BankAccount settlementAccount, long currentTick)
    {
        settlementAccount.Balance += order.ReservedCashRemaining;
        CancelOrderWithoutRelease(order, currentTick);
    }

    private static Shareholding? FindHolding(
        IReadOnlyCollection<Shareholding> shareholdings,
        Guid companyId,
        Guid? ownerPlayerId,
        Guid? ownerCompanyId)
    {
        return shareholdings.FirstOrDefault(holding =>
            holding.CompanyId == companyId
            && holding.OwnerPlayerId == ownerPlayerId
            && holding.OwnerCompanyId == ownerCompanyId);
    }

    private static async Task RecordSharePriceHistoryAsync(
        AppDbContext db,
        Guid companyId,
        decimal sharePrice,
        long currentTick,
        CancellationToken ct)
    {
        var latest = await db.SharePriceHistoryEntries
            .Where(entry => entry.CompanyId == companyId && entry.RecordedAtTick == currentTick)
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (latest is null)
        {
            db.SharePriceHistoryEntries.Add(new SharePriceHistoryEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SharePrice = sharePrice,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
            return;
        }

        latest.SharePrice = sharePrice;
        latest.RecordedAtUtc = DateTime.UtcNow;
    }

    private static void RecordPersonalTradeIfNeeded(
        AppDbContext db,
        LimitOrder order,
        Company company,
        int shareCount,
        decimal tradePrice,
        long currentTick,
        string direction)
    {
        if (!order.OwnerPlayerId.HasValue)
        {
            return;
        }

        var totalValue = decimal.Round(tradePrice * shareCount, 4, MidpointRounding.AwayFromZero);
        db.PersonTradeRecords.Add(new PersonTradeRecord
        {
            Id = Guid.NewGuid(),
            PlayerId = order.OwnerPlayerId.Value,
            CompanyId = company.Id,
            Direction = direction,
            ShareCount = shareCount,
            PricePerShare = tradePrice,
            TotalValue = totalValue,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }

    private static void RecordCompanyLedgerIfNeeded(
        AppDbContext db,
        LimitOrder order,
        Company company,
        int shareCount,
        decimal tradePrice,
        long currentTick,
        bool isBuy)
    {
        if (!order.OwnerCompanyId.HasValue)
        {
            return;
        }

        var totalValue = decimal.Round(tradePrice * shareCount, 4, MidpointRounding.AwayFromZero);
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = order.OwnerCompanyId.Value,
            BankAccountId = order.SettlementBankAccountId,
            Category = isBuy ? LedgerCategory.StockPurchase : LedgerCategory.StockSale,
            Description = isBuy
                ? $"Limit-order buy {shareCount} shares in {company.Name} @ {tradePrice:0.0000}"
                : $"Limit-order sell {shareCount} shares in {company.Name} @ {tradePrice:0.0000}",
            Amount = isBuy ? -totalValue : totalValue,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }
}
