using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<LimitOrderResult> PlaceLimitOrder(
        PlaceLimitOrderInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (input.Quantity <= 0)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Order quantity must be greater than zero.").SetCode("INVALID_ORDER_QUANTITY").Build());
        }

        if (input.LimitPrice <= 0m)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Limit price must be greater than zero.").SetCode("INVALID_LIMIT_PRICE").Build());
        }

        var side = input.Side.Trim().ToUpperInvariant();
        if (side is not (LimitOrderSide.Buy or LimitOrderSide.Sell))
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Order side must be BUY or SELL.").SetCode("INVALID_ORDER_SIDE").Build());
        }

        if (!StockSymbolCodec.TryParseCompanyId(input.StockSymbol, out var companyId))
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Invalid stock symbol.").SetCode("INVALID_STOCK_SYMBOL").Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Player not found.").SetCode("PLAYER_NOT_FOUND").Build());
        var account = await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext.User);
        var currentTick = await GetCurrentTickAsync(db);
        var company = await db.Companies.FirstOrDefaultAsync(candidate => candidate.Id == companyId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Company not found.").SetCode("COMPANY_NOT_FOUND").Build());

        var governmentCompanyIds = await GovernmentCompanyQueries.GetGovernmentCompanyIdsAsync(db);
        if (IsGovernmentCompany(governmentCompanyIds, company))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }

        var settlementAccount = await ResolveDefaultUsdSettlementAccountAsync(db, player, account);
        var limitPrice = decimal.Round(input.LimitPrice, 4, MidpointRounding.AwayFromZero);
        var quantity = input.Quantity;
        var ownerPlayerId = account.Company is null ? player.Id : (Guid?)null;
        var ownerCompanyId = account.Company?.Id;

        if (side == LimitOrderSide.Buy)
        {
            var reserveRequired = StockLimitOrderMatchingService.ComputeReservedBuyCash(limitPrice, quantity);
            if (settlementAccount.Balance < reserveRequired)
            {
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Insufficient selected account balance to reserve this buy limit order.").SetCode("INSUFFICIENT_FUNDS").Build());
            }

            settlementAccount.Balance -= reserveRequired;
        }
        else
        {
            var holding = await db.Shareholdings.FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == companyId
                && candidate.OwnerPlayerId == ownerPlayerId
                && candidate.OwnerCompanyId == ownerCompanyId);
            var ownedShares = holding?.ShareCount ?? 0m;
            var reservedShares = await StockLimitOrderMatchingService.GetReservedSellSharesAsync(
                db,
                companyId,
                ownerPlayerId,
                ownerCompanyId);
            if (ownedShares < quantity + reservedShares)
            {
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Insufficient shares for this sell limit order.").SetCode("INSUFFICIENT_SHARES").Build());
            }
        }

        var order = new LimitOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            StockSymbol = StockSymbolCodec.FromCompanyId(companyId),
            Side = side,
            LimitPrice = limitPrice,
            Quantity = quantity,
            FilledQuantity = 0,
            Status = LimitOrderStatus.Open,
            OwnerPlayerId = ownerPlayerId,
            OwnerCompanyId = ownerCompanyId,
            SettlementBankAccountId = settlementAccount.Id,
            ReservedCashRemaining = side == LimitOrderSide.Buy ? StockLimitOrderMatchingService.ComputeReservedBuyCash(limitPrice, quantity) : 0m,
            CreatedAtTick = currentTick,
            UpdatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.LimitOrders.Add(order);
        await StockLimitOrderMatchingService.MatchForCompanyAsync(db, companyId, currentTick);
        await db.SaveChangesAsync();
        return await BuildLimitOrderResultAsync(db, order.Id);
    }

    [Authorize]
    public async Task<LimitOrderResult> CancelLimitOrder(
        Guid orderId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players
            .Include(candidate => candidate.Companies)
            .FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Player not found.").SetCode("PLAYER_NOT_FOUND").Build());

        var order = await db.LimitOrders
            .Include(candidate => candidate.Company)
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Order not found.").SetCode("ORDER_NOT_FOUND").Build());

        if (order.Status is LimitOrderStatus.Filled or LimitOrderStatus.Cancelled)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Filled or cancelled orders cannot be cancelled again.").SetCode("ORDER_NOT_CANCELLABLE").Build());
        }

        var companyIds = player.Companies.Select(company => company.Id).ToHashSet();
        var isOwner = order.OwnerPlayerId == player.Id || (order.OwnerCompanyId.HasValue && companyIds.Contains(order.OwnerCompanyId.Value));
        if (!isOwner)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("You can cancel only your own limit orders.").SetCode("ORDER_NOT_OWNED").Build());
        }

        var settlementAccount = await db.BankAccounts.FirstOrDefaultAsync(candidate => candidate.Id == order.SettlementBankAccountId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Settlement account not found.").SetCode("BANK_ACCOUNT_NOT_FOUND").Build());

        var currentTick = await GetCurrentTickAsync(db);
        StockLimitOrderMatchingService.CancelOrderAndRelease(order, settlementAccount, currentTick);
        await db.SaveChangesAsync();

        return await BuildLimitOrderResultAsync(db, order.Id);
    }

    private static async Task<BankAccount> ResolveDefaultUsdSettlementAccountAsync(
        AppDbContext db,
        Player player,
        ActiveTradingAccount account)
    {
        BankAccount? usdAccount;
        if (account.Company is null)
        {
            usdAccount = await db.BankAccounts
                .FirstOrDefaultAsync(candidate =>
                    candidate.PlayerId == player.Id
                    && candidate.ClosedAtUtc == null
                    && candidate.CurrencyCode == "USD");
        }
        else
        {
            usdAccount = await db.BankAccounts
                .FirstOrDefaultAsync(candidate =>
                    candidate.CompanyId == account.Company.Id
                    && candidate.ClosedAtUtc == null
                    && candidate.CurrencyCode == "USD");
        }

        if (usdAccount is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A USD settlement account is required to place stock limit orders.")
                    .SetCode("BANK_ACCOUNT_REQUIRED")
                    .Build());
        }

        return usdAccount;
    }

    private static async Task<LimitOrderResult> BuildLimitOrderResultAsync(AppDbContext db, Guid orderId)
    {
        var order = await db.LimitOrders
            .AsNoTracking()
            .Include(candidate => candidate.Company)
            .FirstAsync(candidate => candidate.Id == orderId);

        return new LimitOrderResult
        {
            Id = order.Id,
            CompanyId = order.CompanyId,
            CompanyName = order.Company.Name,
            StockSymbol = order.StockSymbol,
            Side = order.Side,
            LimitPrice = order.LimitPrice,
            Quantity = order.Quantity,
            FilledQuantity = order.FilledQuantity,
            RemainingQuantity = Math.Max(0, order.Quantity - order.FilledQuantity),
            Status = order.Status,
            CreatedAtTick = order.CreatedAtTick,
            UpdatedAtTick = order.UpdatedAtTick,
        };
    }
}
